using System;
using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;

namespace TripMind.Application.Interfaces
{
    public interface IPasswordService
    {
        Task SendPasswordResetOtpAsync(ForgotPasswordRequest req, string? ip = null);

        Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest req, string? ip = null);

        Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest req, string? ip = null);

        Task<MessageResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest req, string? ip = null);
    }
}
