using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;

namespace TripMind.Application.Interfaces
{
    public interface IEmailVerificationService
    {
        Task<MessageResponse> VerifyEmailAsync(VerifyEmailOtpRequest req, string? ip = null);

        Task<MessageResponse> ResendEmailOtpAsync(ResendEmailOtpRequest req, string? ip = null);
    }
}
