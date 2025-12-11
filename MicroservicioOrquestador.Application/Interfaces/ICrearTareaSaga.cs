using System.Threading.Tasks;
using MicroservicioReportes.Application.DTOs;
using MicroservicioReportes.Domain.Entities;

namespace MicroservicioReportes.Application.Interfaces
{
    public interface ICrearTareaSaga
    {
        Task<SagaResult> ExecuteAsync(CrearTareaConEmpleadosRequest request);
    }
}