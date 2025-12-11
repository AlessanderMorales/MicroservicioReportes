using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroservicioReportes.Application.DTOs;
using MicroservicioReportes.Application.Interfaces;
using MicroservicioReportes.Domain.Entities;

namespace MicroservicioReportes.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SagaController : ControllerBase
    {
        private readonly ICrearTareaSaga _saga;

        public SagaController(ICrearTareaSaga saga)
        {
            _saga = saga;
        }

        [HttpPost("crear-tarea-con-empleados")]
        public async Task<IActionResult> CrearTarea([FromBody] CrearTareaConEmpleadosRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(SagaResult.Fail("Datos inválidos"));
            }

            var result = await _saga.ExecuteAsync(request);

            if (result.Success)
            {
                return Ok(new { error = false, message = result.Message, data = result.Data });
            }
            else
            {
                return BadRequest(new { error = true, message = result.Message, errors = result.Errors });
            }
        }
    }
}