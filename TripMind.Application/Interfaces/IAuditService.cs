using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Application.Interfaces
{
    public  interface IAuditService
    {
        void Log(
       Guid? userId,
       string eventType,
       string? ipAddress,
       bool success,
       string? details = null);
    }
}
