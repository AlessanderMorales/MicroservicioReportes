using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MicroservicioReportes.Application.DTOs;
using MicroservicioReportes.Application.Interfaces;
using MicroservicioReportes.Domain.Entities;

namespace MicroservicioReportes.Application.UseCases.Sagas
{
    public class CrearTareaSaga : ICrearTareaSaga
    {
        private readonly IUsuarioServiceExternal _usuarioService;
        private readonly ITareaServiceExternal _tareaService;
        private readonly ILogger<CrearTareaSaga> _logger;

        public CrearTareaSaga(
            IUsuarioServiceExternal usuarioService,
            ITareaServiceExternal tareaService,
            ILogger<CrearTareaSaga> logger)
        {
            _usuarioService = usuarioService;
            _tareaService = tareaService;
            _logger = logger;
        }

        public async Task<SagaResult> ExecuteAsync(CrearTareaConEmpleadosRequest request)
        {
            var compensaciones = new Stack<Func<Task>>();
            var empleadosAsignados = new List<int>();
            int? tareaCreadaId = null;

            try
            {
                _logger.LogInformation("?? [Saga] Iniciando proceso...");

                // 1. Validar
                foreach (var empId in request.EmpleadosIds)
                {
                    var disponible = await _usuarioService.VerificarDisponibilidad(empId);
                    if (!disponible)
                        throw new Exception($"El empleado {empId} no está disponible.");
                }

                // 2. Crear Tarea
                var nuevaTareaDto = new CrearTareaExternalDto
                {
                    titulo = request.Titulo,
                    descripcion = request.Descripcion,
                    prioridad = request.Prioridad,
                    idProyecto = request.IdProyecto,
                    status = request.Status
                };

                var tareaCreada = await _tareaService.CrearTarea(nuevaTareaDto);
                if (tareaCreada == null || tareaCreada.Id <= 0)
                    throw new Exception("Error al crear la tarea en el servicio externo.");

                tareaCreadaId = tareaCreada.Id;

                // Rollback Tarea
                compensaciones.Push(async () => {
                    _logger.LogWarning($"?? [Rollback] Eliminando tarea {tareaCreadaId}");
                    await _tareaService.EliminarTarea(tareaCreadaId.Value);
                });

                // 3. Asignar
                var asignacionOk = await _tareaService.AsignarEmpleados(tareaCreadaId.Value, request.EmpleadosIds);
                if (!asignacionOk) throw new Exception("Error al asignar empleados a la tarea.");

                // 4. Marcar Ocupados
                foreach (var empId in request.EmpleadosIds)
                {
                    var ocupadoOk = await _usuarioService.MarcarOcupado(empId);
                    if (ocupadoOk)
                    {
                        empleadosAsignados.Add(empId);
                        // Rollback Empleado
                        compensaciones.Push(async () => {
                            _logger.LogWarning($"?? [Rollback] Liberando empleado {empId}");
                            await _usuarioService.MarcarDisponible(empId);
                        });
                    }
                    else
                    {
                        throw new Exception($"No se pudo marcar como ocupado al empleado {empId}");
                    }
                }

                return SagaResult.Ok(new { TareaId = tareaCreadaId, Empleados = empleadosAsignados.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError($"? [Saga Falló] {ex.Message}");
                while (compensaciones.Count > 0)
                {
                    try { await compensaciones.Pop()(); }
                    catch (Exception rbEx) { _logger.LogError($"Error en rollback: {rbEx.Message}"); }
                }
                return SagaResult.Fail(ex.Message);
            }
        }
    }
}