using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MicroservicioReportes.Application.Interfaces;
using MicroservicioReportes.Application.UseCases.Sagas;
using MicroservicioReportes.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CONFIGURACIÓN DE HTTP CLIENTS ---
builder.Services.AddHttpClient("UsuarioClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5001/");
});

builder.Services.AddHttpClient("TareaClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5003/");
});

// --- INYECCIÓN DE DEPENDENCIAS ---

// Infraestructura (Implementación técnica)
builder.Services.AddScoped<IUsuarioServiceExternal, UsuarioServiceExternal>();
builder.Services.AddScoped<ITareaServiceExternal, TareaServiceExternal>();

// Aplicación (Lógica de Negocio / Saga)
builder.Services.AddScoped<ICrearTareaSaga, CrearTareaSaga>();


// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

app.Run();