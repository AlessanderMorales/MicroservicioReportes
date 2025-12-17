using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

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

        public ReporteConsumer(
            IConfiguration configuration,
            ILogger<ReporteConsumer> logger)
        {
            _logger = logger;

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
                        "Evento recibido: TareaId={TareaId}, Titulo={Titulo}, Empleados={Count}, Usuario={Usuario}",
                        evento.TareaId,
                        evento.TareaTitulo,
                        evento.EmpleadosIds.Count,
                        evento.UsuarioNombre
                    );

                    _logger.LogInformation(
                        "Evento procesado: Reporte para tarea {TareaId} con {Count} empleados",
                        evento.TareaId,
                        evento.EmpleadosIds.Count
                    );

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
