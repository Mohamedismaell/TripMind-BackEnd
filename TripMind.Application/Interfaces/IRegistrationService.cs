using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;

namespace TripMind.Application.Interfaces
{
    public interface IRegistrationService
    {
        public Task<MessageResponse> RegisterAsync(RegisterRequest req, string? ip = null);
    }
}
