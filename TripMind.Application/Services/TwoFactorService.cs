using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;
using TripMind.Application.Exceptions;
using TripMind.Application.Interfaces;

namespace TripMind.Application.Services
{
    public sealed class TwoFactorService : ITwoFactorService
    {
        private readonly IAppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly IEmailSender _email;
        private readonly IOtpService _otpService;
        private readonly IAuditService _auditService;

        public TwoFactorService(
            IAppDbContext db,
            IPasswordHasher hasher,
            IEmailSender email,
            IOtpService otpService,
            IAuditService auditService)
        {
            _db = db;
            _hasher = hasher;
            _email = email;
            _otpService = otpService;
            _auditService = auditService;
        }

        public async Task<MessageResponse> InitiateTwoFactorAsync(Guid userId, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
                ?? throw new NotFoundAppException("User not found.");

            if (user.TwoFactorEnabled)
                throw new ConflictAppException("2FA is already enabled.");

            string otp = _otpService.GenerateOtp();
            user.TwoFactorOtp = _hasher.Hash(otp);
            user.TwoFactorOtpExpiry = DateTime.UtcNow.AddMinutes(10);
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _email.SendTwoFactorOtpAsync(user.Email, user.DisplayName, otp);

            return new MessageResponse { Message = "OTP sent to your email. Please confirm to enable 2FA." };
        }

        public async Task<MessageResponse> ConfirmTwoFactorAsync(Guid userId, TwoFactorConfirmRequest req, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
                ?? throw new NotFoundAppException("User not found.");

            if (user.TwoFactorOtpExpiry == null || user.TwoFactorOtpExpiry < DateTime.UtcNow)
                throw new BadRequestAppException("OTP has expired. Please initiate 2FA again.");

            if (user.TwoFactorOtp == null || !_hasher.Verify(req.Otp, user.TwoFactorOtp))
                throw new BadRequestAppException("Invalid OTP.");

            user.TwoFactorEnabled = true;
            user.TwoFactorOtp = null;
            user.TwoFactorOtpExpiry = null;
            user.UpdatedAt = DateTime.UtcNow;

            _auditService.Log(userId, "AUTH.2FA_ENABLED", ip, true);
            await _db.SaveChangesAsync();

            return new MessageResponse { Message = "Two-factor authentication has been enabled." };
        }

        public async Task<MessageResponse> DisableTwoFactorAsync(Guid userId, TwoFactorDisableRequest req, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
                ?? throw new NotFoundAppException("User not found.");

            if (!user.TwoFactorEnabled)
                throw new ConflictAppException("2FA is not enabled.");

            if (!_hasher.Verify(req.Password, user.PasswordHash))
                throw new UnauthorizedAppException("Invalid password.");

            user.TwoFactorEnabled = false;
            user.TwoFactorOtp = null;
            user.TwoFactorOtpExpiry = null;
            user.UpdatedAt = DateTime.UtcNow;

            _auditService.Log(userId, "AUTH.2FA_DISABLED", ip, true);
            await _db.SaveChangesAsync();

            return new MessageResponse { Message = "Two-factor authentication has been disabled." };
        }

        public async Task<MessageResponse> ResendTwoFactorOtpAsync(string email, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant().Trim());
            if (user == null) { await Task.Delay(200); return new MessageResponse { Message = "If valid, a new OTP has been sent." }; }

            string otp = _otpService.GenerateOtp();
            user.TwoFactorOtp = _hasher.Hash(otp);
            user.TwoFactorOtpExpiry = DateTime.UtcNow.AddMinutes(10);
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _email.SendTwoFactorOtpAsync(user.Email, user.DisplayName, otp);

            return new MessageResponse { Message = "If valid, a new OTP has been sent." };
        }
    }
}
