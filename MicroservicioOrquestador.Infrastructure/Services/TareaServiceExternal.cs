using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MicroservicioReportes.Application.DTOs;
using MicroservicioReportes.Application.Interfaces;

namespace MicroservicioReportes.Infrastructure.Services
{
    public class TareaServiceExternal : ITareaServiceExternal
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public TareaServiceExternal(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("TareaClient");
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public async Task<TareaResponseDto?> CrearTarea(CrearTareaExternalDto tareaDto)
        {
            try
            {
                var json = JsonSerializer.Serialize(tareaDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/tarea", content);
                if (!response.IsSuccessStatusCode) return null;

                var responseString = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseString);
                if (doc.RootElement.TryGetProperty("data", out var dataElem))
                {
                    return JsonSerializer.Deserialize<TareaResponseDto>(dataElem.GetRawText(), _jsonOptions);
                }
                return null;
            }
            catch { return null; }
        }

        public async Task<bool> AsignarEmpleados(int tareaId, List<int> empleadosIds)
        {
            try
            {
                var json = JsonSerializer.Serialize(empleadosIds);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"api/tarea/{tareaId}/usuarios", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task EliminarTarea(int tareaId)
        {
            try { await _httpClient.DeleteAsync($"api/tarea/{tareaId}"); }
            catch { }
        }
    }
}