using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TripMind.Application.Interfaces;
using TripMind.Domain.Entities;

namespace TripMind.Application.Services
{
    public sealed class AuditService : IAuditService
    {
        private readonly IAppDbContext _db;

        public AuditService(IAppDbContext db)
        {
            _db = db;
        }

        public void Log(
            Guid? userId,
            string eventType,
            string? ipAddress,
            bool success,
            string? details = null)
        {
            var audit = new AuditLog
            {
                AuditLogId = Guid.NewGuid(),
                UserId = userId,
                EventType = eventType,
                IpAddress = ipAddress,
                Success = success,
                Details = details,
                CreatedAt = DateTime.UtcNow
            };

            _db.AuditLogs.Add(audit);
        }
    }
}
