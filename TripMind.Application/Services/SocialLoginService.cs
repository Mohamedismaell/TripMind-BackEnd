using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;
using TripMind.Application.Exceptions;
using TripMind.Application.Interfaces;
using TripMind.Domain.Entities;

namespace TripMind.Application.Services
{
    public sealed class SocialLoginService : ISocialLoginService
    {
        private readonly IAppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IAuditService _auditService;
        private readonly ITokenService _tokenService;

        public SocialLoginService(
            IAppDbContext db,
            IPasswordHasher hasher,
            IHttpClientFactory httpFactory,
            IAuditService auditService,
            ITokenService tokenService)
        {
            _db = db;
            _hasher = hasher;
            _httpFactory = httpFactory;
            _auditService = auditService;
            _tokenService = tokenService;
        }

        public async Task<TokenResponse> GoogleLoginAsync(string idToken, string? ip = null)
        {
            Google.Apis.Auth.GoogleJsonWebSignature.Payload payload;
            try { payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(idToken); }
            catch { throw new UnauthorizedAppException("Invalid Google token."); }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject)
                    ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == payload.Email.ToLowerInvariant());

            if (user == null)
            {
                var now = DateTime.UtcNow;
                user = new User
                {
                    UserId = Guid.NewGuid(),
                    Email = payload.Email.ToLowerInvariant(),
                    DisplayName = payload.Name ?? payload.Email,
                    ProfilePhotoUrl = payload.Picture,
                    GoogleId = payload.Subject,
                    PasswordHash = _hasher.Hash(Guid.NewGuid().ToString()),
                    IsActive = true,
                    IsEmailVerified = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Users.Add(user);
            }
            else
            {
                if (user.GoogleId == null) user.GoogleId = payload.Subject;
                if (user.ProfilePhotoUrl == null) user.ProfilePhotoUrl = payload.Picture;
                user.IsEmailVerified = true;
                user.UpdatedAt = DateTime.UtcNow;
            }

            if (!user.IsActive)
                throw new ForbiddenAppException("Your account has been deactivated. Please contact support.");

            _auditService.Log(user.UserId, "AUTH.GOOGLE_LOGIN", ip, true);
            await _db.SaveChangesAsync();

            return await _tokenService.IssueTokenPairAsync(user, ip);
        }

        public async Task<TokenResponse> FacebookLoginAsync(string accessToken, string? ip = null)
        {
            var http = _httpFactory.CreateClient("Facebook");

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://graph.facebook.com/me?fields=id,name,email,picture.type(large)");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var res = await http.SendAsync(request);

            if (!res.IsSuccessStatusCode)
                throw new UnauthorizedAppException("Invalid Facebook token.");

            var json = await res.Content.ReadAsStringAsync();

            var fb = System.Text.Json.JsonSerializer.Deserialize<FacebookUserInfo>(
                json,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
                ?? throw new ServiceUnavailableAppException("Could not parse Facebook response.");

            if (string.IsNullOrEmpty(fb.Email))
                throw new BadRequestAppException("Facebook account has no email. Please use email registration.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.FacebookId == fb.Id)
                ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == fb.Email.ToLowerInvariant());

            if (user == null)
            {
                var now = DateTime.UtcNow;

                user = new User
                {
                    UserId = Guid.NewGuid(),
                    Email = fb.Email.ToLowerInvariant(),
                    DisplayName = fb.Name ?? fb.Email,
                    ProfilePhotoUrl = fb.Picture?.Data?.Url,
                    FacebookId = fb.Id,
                    PasswordHash = _hasher.Hash(Guid.NewGuid().ToString()),
                    IsActive = true,
                    IsEmailVerified = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _db.Users.Add(user);
            }
            else
            {
                if (user.FacebookId == null)
                    user.FacebookId = fb.Id;

                if (!string.IsNullOrWhiteSpace(fb.Picture?.Data?.Url))
                {
                    user.ProfilePhotoUrl = fb.Picture.Data.Url;
                }
                user.IsEmailVerified = true;
                user.UpdatedAt = DateTime.UtcNow;
            }

            if (!user.IsActive)
                throw new ForbiddenAppException("Your account has been deactivated. Please contact support.");

            _auditService.Log(user.UserId, "AUTH.FACEBOOK_LOGIN", ip, true);

            await _db.SaveChangesAsync();

            return await _tokenService.IssueTokenPairAsync(user, ip);
        }
    }

    internal sealed class FacebookUserInfo
    {
        public string Id { get; set; } = null!;
        public string? Name { get; set; }
        public string? Email { get; set; }
        public FacebookPicture? Picture { get; set; }
    }

    internal sealed class FacebookPicture
    {
        public FacebookPictureData? Data { get; set; }
    }

    internal sealed class FacebookPictureData
    {
        public string? Url { get; set; }
    }
}
