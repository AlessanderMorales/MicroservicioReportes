using System;
using System.Collections.Generic;

namespace MicroservicioReportes.Domain.Entities
{
    public class SagaResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public static SagaResult Fail(string message, List<string>? errors = null)
        {
            return new SagaResult
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string> { message }
            };
        }

        public static SagaResult Ok(object? data = null, string message = "Operación exitosa")
        {
            return new SagaResult
            {
                Success = true,
                Message = message,
                Data = data
            };
        }
    }
}