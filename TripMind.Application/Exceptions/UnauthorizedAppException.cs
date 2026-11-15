using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Application.Exceptions
{
    public sealed class   UnauthorizedAppException(string message) : AppException(message, 401, "UNAUTHORIZED")
    {
    }
}
