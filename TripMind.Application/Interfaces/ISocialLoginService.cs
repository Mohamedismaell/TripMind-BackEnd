using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;

namespace TripMind.Application.Interfaces
{
    public interface ISocialLoginService
    {
        Task<TokenResponse> GoogleLoginAsync(string idToken, string? ip = null);

        Task<TokenResponse> FacebookLoginAsync(string accessToken, string? ip = null);
    }
}
