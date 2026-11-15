using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Application.Exceptions
{
    public sealed class BadRequestAppException : AppException
    {
        public BadRequestAppException(string message)
            : base(message, 400, "BAD_REQUEST")
        {
        }
    }
}
