using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MicroservicioReportes.Application.Interfaces;

namespace MicroservicioReportes.Infrastructure.Services
{
    public class UsuarioServiceExternal : IUsuarioServiceExternal
    {
        private readonly HttpClient _httpClient;

        public UsuarioServiceExternal(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("UsuarioClient");
        }

        public async Task<bool> VerificarDisponibilidad(int empleadoId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/usuario/{empleadoId}/disponible");
                if (!response.IsSuccessStatusCode) return false;

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("disponible", out var elem) ||
                    doc.RootElement.TryGetProperty("Disponible", out elem))
                {
                    return elem.GetBoolean();
                }
                return false;
            }
            catch { return false; }
        }

        public async Task<bool> MarcarOcupado(int empleadoId)
        {
            try
            {
                var response = await _httpClient.PutAsync($"api/usuario/{empleadoId}/marcar-ocupado", null);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> MarcarDisponible(int empleadoId)
        {
            try
            {
                var response = await _httpClient.PutAsync($"api/usuario/{empleadoId}/marcar-disponible", null);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<dynamic> ObtenerUsuarioPorId(int usuarioId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/usuario/{usuarioId}");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                var usuario = JsonSerializer.Deserialize<dynamic>(content);
                return usuario;
            }
            catch
            {
                return null;
            }
        }
    }
}