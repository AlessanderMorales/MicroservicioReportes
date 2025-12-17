using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using MicroservicioReportes.Application.Services;

namespace MicroservicioReportes.Application.Messaging
{
    public class TareaAsignadaEvent
    {
        public int TareaId { get; set; }
        public List<int> EmpleadosIds { get; set; } = new();
        public string UsuarioNombre { get; set; } = string.Empty;
        public DateTime FechaEvento { get; set; }
        public string TareaTitulo { get; set; } = string.Empty;
    }

    public class ReporteConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly ILogger<ReporteConsumer> _logger;
        private readonly string _queueName;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public ReporteConsumer(
            IConfiguration configuration,
            ILogger<ReporteConsumer> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;

            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                    Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                    UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                    Password = configuration["RabbitMQ:Password"] ?? "guest",
                    VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/"
                };

                _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
                _queueName = configuration["RabbitMQ:QueueName"] ?? "reportes.tarea.asignada";

                var exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "tareas.events";

                _channel.ExchangeDeclareAsync(
                    exchange: exchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false
                ).GetAwaiter().GetResult();

                _channel.QueueDeclareAsync(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false
                ).GetAwaiter().GetResult();

                _channel.QueueBindAsync(
                    queue: _queueName,
                    exchange: exchangeName,
                    routingKey: "tarea.asignada"
                ).GetAwaiter().GetResult();

                _logger.LogInformation("RabbitMQ Consumer conectado exitosamente a cola: {QueueName}", _queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al conectar Consumer con RabbitMQ");
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReporteConsumer iniciado, esperando eventos...");

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var evento = JsonSerializer.Deserialize<TareaAsignadaEvent>(message);

                    if (evento == null)
                    {
                        _logger.LogWarning("Evento deserializado es nulo");
                        await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
                        return;
                    }

                    _logger.LogInformation(
                        "?? Evento recibido: TareaId={TareaId}, Titulo={Titulo}, Empleados={Count}, Usuario={Usuario}",
                        evento.TareaId,
                        evento.TareaTitulo,
                        evento.EmpleadosIds.Count,
                        evento.UsuarioNombre
                    );

                    if (evento.EmpleadosIds.Count > 0)
                    {
                        await GenerarReportesAsync(evento);
                    }
                    else
                    {
                        _logger.LogInformation("No se generan reportes: sin empleados asignados");
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error al deserializar evento JSON");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al procesar evento");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: _queueName,
                autoAck: false,
                consumer: consumer
            );

            _logger.LogInformation("Consumer registrado en cola: {QueueName}", _queueName);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("ReporteConsumer detenido");
            }
        }

        private async Task GenerarReportesAsync(TareaAsignadaEvent evento)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                try
                {
                    var reporteService = scope.ServiceProvider.GetRequiredService<ReporteGeneratorService>();

                    _logger.LogInformation("?? Generando reportes automáticos (PDF y Excel) para tarea {TareaId}", evento.TareaId);

                    var (pdfBytes, excelBytes) = await reporteService.GenerarReportesTareaAsync(
                        evento.TareaId,
                        evento.TareaTitulo,
                        evento.EmpleadosIds,
                        evento.UsuarioNombre
                    );

                    var rutaBase = _configuration["Reportes:RutaBase"] ?? Directory.GetCurrentDirectory();
                    var directorioRaiz = Directory.GetParent(rutaBase)?.Parent?.Parent?.FullName;

                    if (string.IsNullOrEmpty(directorioRaiz))
                    {
                        directorioRaiz = rutaBase;
                    }

                    var rutaReportes = Path.Combine(directorioRaiz, "reportes");
                    Directory.CreateDirectory(rutaReportes);

                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                    var nombreArchivoPdf = $"Tarea_{evento.TareaId}_{timestamp}.pdf";
                    var rutaCompletaPdf = Path.Combine(rutaReportes, nombreArchivoPdf);
                    await File.WriteAllBytesAsync(rutaCompletaPdf, pdfBytes);
                    _logger.LogInformation("? Reporte PDF guardado en: {Ruta}", rutaCompletaPdf);

                    var nombreArchivoExcel = $"Tarea_{evento.TareaId}_{timestamp}.xlsx";
                    var rutaCompletaExcel = Path.Combine(rutaReportes, nombreArchivoExcel);
                    await File.WriteAllBytesAsync(rutaCompletaExcel, excelBytes);
                    _logger.LogInformation("? Reporte Excel guardado en: {Ruta}", rutaCompletaExcel);

                    _logger.LogInformation("?? Reportes generados exitosamente para tarea {TareaId}", evento.TareaId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "? Error al generar reportes para tarea {TareaId}", evento.TareaId);
                    throw;
                }
            }
        }

        public override void Dispose()
        {
            try
            {
                _channel?.CloseAsync().GetAwaiter().GetResult();
                _connection?.CloseAsync().GetAwaiter().GetResult();
                _logger.LogInformation("RabbitMQ Consumer desconectado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar conexiones de RabbitMQ");
            }

            base.Dispose();
        }
    }
}

