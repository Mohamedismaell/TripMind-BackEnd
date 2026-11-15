using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;
using TripMind.Application.Exceptions;
using TripMind.Application.Interfaces;
using TripMind.Domain.Entities;

namespace TripMind.Application.Services
{
    public sealed class TokenService : ITokenService
    {
        private readonly IAppDbContext _db;
        private readonly IJwtProvider _jwt;
        private readonly IAuditService _auditService;

        public TokenService(
            IAppDbContext db,
            IJwtProvider jwt,
            IAuditService auditService)
        {
            _db = db;
            _jwt = jwt;
            _auditService = auditService;
        }

        public async Task<TokenResponse> IssueTokenPairAsync(User user, string? ip)
        {
            var stale = await _db.RefreshTokens
                .Where(r => r.UserId == user.UserId && !r.IsRevoked && r.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();
            foreach (var t in stale) t.IsRevoked = true;

            var rawRefresh = _jwt.GenerateRefreshToken();
            _db.RefreshTokens.Add(new RefreshToken
            {
                RefreshTokenId = Guid.NewGuid(),
                UserId = user.UserId,
                Token = rawRefresh,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwt.GetRefreshTokenLifetimeDays(user.RememberMe)),
                CreatedByIp = ip,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            var (access, expiresIn) = _jwt.GenerateAccessToken(user);
            return ToResponse(user, access, expiresIn, rawRefresh);
        }

        public async Task<TokenResponse> RefreshAsync(string refreshToken, string? ip = null)
        {
            var stored = await _db.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == refreshToken);

            if (stored == null || !stored.IsActive)
                throw new UnauthorizedAppException("Invalid or expired refresh token.");

            var newRaw = _jwt.GenerateRefreshToken();
            stored.IsRevoked = true;
            stored.ReplacedByToken = newRaw;

            _db.RefreshTokens.Add(new RefreshToken
            {
                RefreshTokenId = Guid.NewGuid(),
                UserId = stored.UserId,
                Token = newRaw,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwt.GetRefreshTokenLifetimeDays(stored.User.RememberMe)),
                CreatedByIp = ip,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            var (access, expiresIn) = _jwt.GenerateAccessToken(stored.User);
            return ToResponse(stored.User, access, expiresIn, newRaw);
        }

        public async Task RevokeAsync(string refreshToken)
        {
            var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
            if (stored == null || !stored.IsActive) throw new UnauthorizedAppException("Token not found or already revoked.");
            stored.IsRevoked = true;
            await _db.SaveChangesAsync();
        }

        public async Task LogoutAsync(Guid userId, string refreshToken)
        {
            var token = await _db.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == refreshToken && r.UserId == userId);

            if (token != null && token.IsActive)
                token.IsRevoked = true;

            _auditService.Log(userId, "AUTH.LOGOUT", null, true);
            await _db.SaveChangesAsync();
        }

        private static TokenResponse ToResponse(User u, string access, int expiresIn, string refresh) => new()
        {
            AccessToken = access,
            ExpiresIn = expiresIn,
            RefreshToken = refresh,
            UserId = u.UserId,
            DisplayName = u.DisplayName,
            Email = u.Email,
            ProfilePhotoUrl = u.ProfilePhotoUrl,
            LanguagePreference = u.LanguagePreference,
            IsEmailVerified = u.IsEmailVerified,
            TwoFactorEnabled = u.TwoFactorEnabled
        };
    }
}
