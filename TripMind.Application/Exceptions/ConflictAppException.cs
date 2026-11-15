using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Application.Exceptions
{
    public sealed class ConflictAppException : AppException
    {
        public ConflictAppException(string message)
            : base(message, 409, "CONFLICT")
        {
        }
    }
}
