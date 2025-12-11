using System.Collections.Generic;

namespace MicroservicioReportes.Application.DTOs
{
    public class CrearTareaConEmpleadosRequest
    {
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string Prioridad { get; set; } = "Media";
        public int IdProyecto { get; set; }
        public string Status { get; set; } = "SinIniciar";
        public List<int> EmpleadosIds { get; set; } = new List<int>();
    }

    public class CrearTareaExternalDto
    {
        public string titulo { get; set; } = string.Empty;
        public string? descripcion { get; set; }
        public string prioridad { get; set; } = string.Empty;
        public int idProyecto { get; set; }
        public string status { get; set; } = string.Empty;
        public int estado { get; set; } = 1;
    }

    public class TareaResponseDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
    }
}