using System;
using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;

namespace TripMind.Application.Interfaces
{
    public interface ITwoFactorService
    {
        Task<MessageResponse> InitiateTwoFactorAsync(Guid userId, string? ip = null);

        Task<MessageResponse> ConfirmTwoFactorAsync(Guid userId, TwoFactorConfirmRequest req, string? ip = null);

        Task<MessageResponse> DisableTwoFactorAsync(Guid userId, TwoFactorDisableRequest req, string? ip = null);

        Task<MessageResponse> ResendTwoFactorOtpAsync(string email, string? ip = null);
    }
}
