using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MicroservicioReportes.Application.Interfaces;
using MicroservicioReportes.Application.UseCases.Sagas;
using MicroservicioReportes.Application.Services;
using MicroservicioReportes.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("UsuarioClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5001/");
});

builder.Services.AddHttpClient("TareaClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5003/");
});

builder.Services.AddScoped<IUsuarioServiceExternal, UsuarioServiceExternal>();
builder.Services.AddScoped<ITareaServiceExternal, TareaServiceExternal>();
builder.Services.AddScoped<ReporteGeneratorService>();

builder.Services.AddScoped<MicroservicioReportes.Application.Repository.ProcessedEventsRepository>();

builder.Services.AddScoped<ICrearTareaSaga, CrearTareaSaga>();

builder.Services.AddHostedService<MicroservicioReportes.Application.Messaging.ReporteConsumer>();

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
