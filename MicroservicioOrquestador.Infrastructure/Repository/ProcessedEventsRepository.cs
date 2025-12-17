using System;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace MicroservicioOrquestador.Infrastructure.Repository
{
    public class ProcessedEventsRepository
    {
        private readonly string _connectionString;

        public ProcessedEventsRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException("ConnectionString 'DefaultConnection' no encontrado");
        }

        public bool IsEventProcessed(string eventId)
        {
            using var conn = new MySqlConnection(_connectionString);
            const string sql = "SELECT COUNT(1) FROM ProcessedEvents WHERE event_id = @EventId";
            return conn.ExecuteScalar<int>(sql, new { EventId = eventId }) > 0;
        }

        public void MarkAsProcessed(string eventId, string eventType)
        {
            using var conn = new MySqlConnection(_connectionString);
            const string sql = @"
                INSERT INTO ProcessedEvents (event_id, event_type, processed_at)
                VALUES (@EventId, @EventType, @ProcessedAt)
                ON DUPLICATE KEY UPDATE processed_at = VALUES(processed_at)";
            
            conn.Execute(sql, new { 
                EventId = eventId, 
                EventType = eventType, 
                ProcessedAt = DateTime.Now 
            });
        }

        public DateTime? GetProcessedDate(string eventId)
        {
            using var conn = new MySqlConnection(_connectionString);
            const string sql = "SELECT processed_at FROM ProcessedEvents WHERE event_id = @EventId";
            return conn.QueryFirstOrDefault<DateTime?>(sql, new { EventId = eventId });
        }
    }
}
