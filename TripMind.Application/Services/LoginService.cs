using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;
using TripMind.Application.Exceptions;
using TripMind.Application.Interfaces;
using TripMind.Domain.Entities;

namespace TripMind.Application.Services
{
    public sealed class LoginService : ILoginService
    {
        private readonly IAppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly IEmailSender _email;
        private readonly IOtpService _otpService;
        private readonly IAuditService _auditService;
        private readonly ITokenService _tokenService;

        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 15;

        public LoginService(
            IAppDbContext db,
            IPasswordHasher hasher,
            IEmailSender email,
            IOtpService otpService,
            IAuditService auditService,
            ITokenService tokenService)
        {
            _db = db;
            _hasher = hasher;
            _email = email;
            _otpService = otpService;
            _auditService = auditService;
            _tokenService = tokenService;
        }

        public async Task<object> LoginAsync(LoginRequest req, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant().Trim());

            if (user != null && user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
                throw new ForbiddenAppException($"Account is locked. Try again after {user.LockoutEnd.Value:HH:mm} UTC.");

            bool ok = user != null && _hasher.Verify(req.Password, user.PasswordHash);

            if (!ok)
            {
                if (user != null)
                {
                    user.FailedLoginAttempts++;
                    if (user.FailedLoginAttempts >= MaxFailedAttempts)
                    {
                        user.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                        user.FailedLoginAttempts = 0;
                    }
                    user.UpdatedAt = DateTime.UtcNow;
                }
                _auditService.Log(user?.UserId, "AUTH.LOGIN", ip, false, $"Failed attempt for: {req.Email}");
                await _db.SaveChangesAsync();
                throw new UnauthorizedAppException("Invalid email or password.");
            }

            if (!user!.IsActive)
                throw new ForbiddenAppException("Your account has been deactivated. Please contact support.");

            if (!user.IsEmailVerified)
                throw new ForbiddenAppException("Please verify your email before logging in.");

            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;

            if (user.TwoFactorEnabled)
            {
                string otp = _otpService.GenerateOtp();
                user.TwoFactorOtp = _hasher.Hash(otp);
                user.TwoFactorOtpExpiry = DateTime.UtcNow.AddMinutes(10);
                user.UpdatedAt = DateTime.UtcNow;
                _auditService.Log(user.UserId, "AUTH.LOGIN_2FA_SENT", ip, true);
                await _db.SaveChangesAsync();
                await _email.SendTwoFactorOtpAsync(user.Email, user.DisplayName, otp);
                return new PendingTwoFactorResponse { Email = user.Email };
            }

            if (user.RememberMe != req.RememberMe)
            {
                user.RememberMe = req.RememberMe;
                user.UpdatedAt = DateTime.UtcNow;
            }

            _auditService.Log(user.UserId, "AUTH.LOGIN", ip, true);
            await _db.SaveChangesAsync();

            return await _tokenService.IssueTokenPairAsync(user, ip);
        }

        public async Task<TokenResponse> VerifyLoginOtpAsync(LoginOtpRequest req, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant().Trim())
                ?? throw new BadRequestAppException("Invalid request.");

            if (user.TwoFactorOtpExpiry == null || user.TwoFactorOtpExpiry < DateTime.UtcNow)
                throw new BadRequestAppException("OTP has expired. Please login again.");

            if (user.TwoFactorOtp == null || !_hasher.Verify(req.Otp, user.TwoFactorOtp))
                throw new BadRequestAppException("Invalid OTP.");

            user.TwoFactorOtp = null;
            user.TwoFactorOtpExpiry = null;
            user.UpdatedAt = DateTime.UtcNow;

            _auditService.Log(user.UserId, "AUTH.LOGIN_2FA_VERIFIED", ip, true);
            await _db.SaveChangesAsync();

            return await _tokenService.IssueTokenPairAsync(user, ip);
        }
    }
}
