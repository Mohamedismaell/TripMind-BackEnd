using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Application.Interfaces
{
    public interface IEmailDomainPolicy
    {
        bool IsBlocked(string email);
    }
}
