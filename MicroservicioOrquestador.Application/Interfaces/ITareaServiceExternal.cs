using System.Collections.Generic;
using System.Threading.Tasks;
using MicroservicioReportes.Application.DTOs;

namespace MicroservicioReportes.Application.Interfaces
{
    public interface ITareaServiceExternal
    {
        Task<TareaResponseDto> CrearTarea(CrearTareaExternalDto tareaDto);
        Task<bool> AsignarEmpleados(int tareaId, List<int> empleadosIds);
        Task EliminarTarea(int tareaId);
    }
}