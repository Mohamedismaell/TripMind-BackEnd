using System;
using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;

namespace TripMind.Application.Interfaces
{
    public interface ILoginService
    {
        Task<object> LoginAsync(LoginRequest req, string? ip = null);

        Task<TokenResponse> VerifyLoginOtpAsync(LoginOtpRequest req, string? ip = null);
    }
}
