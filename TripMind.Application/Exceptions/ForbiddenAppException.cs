using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Application.Exceptions
{

    public sealed class ForbiddenAppException : AppException
    {
        public ForbiddenAppException(string message)
            : base(message, 403, "FORBIDDEN")
        {
        }
    }
}
