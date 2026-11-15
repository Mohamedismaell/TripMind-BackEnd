using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Application.Exceptions
{
    public sealed class ServiceUnavailableAppException : AppException
    {
        public ServiceUnavailableAppException(string message)
            : base(message, 503, "SERVICE_UNAVAILABLE")
        {
        }
    }
}
