using System.Threading.Tasks;

namespace MicroservicioReportes.Application.Interfaces
{
    public interface IUsuarioServiceExternal
    {
        Task<bool> VerificarDisponibilidad(int empleadoId);
        Task<bool> MarcarOcupado(int empleadoId);
        Task<bool> MarcarDisponible(int empleadoId);
        Task<dynamic> ObtenerUsuarioPorId(int usuarioId);
    }
}