using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;
using TripMind.Domain.Entities;

namespace TripMind.Application.Interfaces
{
    public interface ITokenService
    {
        Task<TokenResponse> IssueTokenPairAsync(User user, string? ip);

        Task<TokenResponse> RefreshAsync(string refreshToken, string? ip = null);

        Task RevokeAsync(string refreshToken);

        Task LogoutAsync(Guid userId, string refreshToken);
    }
}
