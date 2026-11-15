using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Application.Exceptions
{

    public sealed class NotFoundAppException : AppException
    {
        public NotFoundAppException(string message)
            : base(message, 404, "NOT_FOUND")
        {
        }
    }
}
