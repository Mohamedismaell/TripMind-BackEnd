using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;
using TripMind.Application.Exceptions;
using TripMind.Application.Interfaces;

namespace TripMind.Application.Services
{
    public sealed class PasswordService : IPasswordService
    {
        private readonly IAppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly IEmailSender _email;
        private readonly IOtpService _otpService;
        private readonly IAuditService _auditService;

        public PasswordService(
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

        public async Task SendPasswordResetOtpAsync(ForgotPasswordRequest req, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant().Trim());
            if (user == null) { await Task.Delay(200); return; }

            string otp = _otpService.GenerateOtp();
            user.PasswordResetToken = _hasher.Hash(otp);
            user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(15);
            user.UpdatedAt = DateTime.UtcNow;

            _auditService.Log(user.UserId, "AUTH.FORGOT_PASSWORD", ip, true);
            await _db.SaveChangesAsync();
            await _email.SendPasswordResetOtpAsync(user.Email, user.DisplayName, otp);
        }

        public async Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest req, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant().Trim())
                ?? throw new BadRequestAppException("Invalid or expired OTP.");

            if (user.ResetTokenExpiry == null || user.ResetTokenExpiry < DateTime.UtcNow)
                throw new BadRequestAppException("OTP has expired. Please request a new one.");

            if (user.PasswordResetToken == null || !_hasher.Verify(req.Otp, user.PasswordResetToken))
                throw new BadRequestAppException("Invalid OTP.");

            string resetToken = Guid.NewGuid().ToString("N");
            user.PasswordResetToken = _hasher.Hash(resetToken);
            user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(10);
            user.UpdatedAt = DateTime.UtcNow;

            _auditService.Log(user.UserId, "AUTH.OTP_VERIFIED", ip, true);
            await _db.SaveChangesAsync();

            return new VerifyOtpResponse { ResetToken = resetToken };
        }

        public async Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest req, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant().Trim())
                ?? throw new BadRequestAppException("Invalid request.");

            if (user.ResetTokenExpiry == null || user.ResetTokenExpiry < DateTime.UtcNow)
                throw new BadRequestAppException("Reset token has expired.");

            if (user.PasswordResetToken == null || !_hasher.Verify(req.ResetToken, user.PasswordResetToken))
                throw new BadRequestAppException("Invalid or already-used reset token.");

            if (_hasher.Verify(req.NewPassword, user.PasswordHash))
                throw new BadRequestAppException("New password must be different from the current password.");

            user.PasswordHash = _hasher.Hash(req.NewPassword);
            user.PasswordResetToken = null;
            user.ResetTokenExpiry = null;
            user.UpdatedAt = DateTime.UtcNow;

            var active = await _db.RefreshTokens
                .Where(r => r.UserId == user.UserId && !r.IsRevoked)
                .ToListAsync();
            foreach (var t in active) t.IsRevoked = true;

            _auditService.Log(user.UserId, "AUTH.PASSWORD_RESET", ip, true);
            await _db.SaveChangesAsync();

            return new ResetPasswordResponse();
        }

        public async Task<MessageResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest req, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
                ?? throw new NotFoundAppException("User not found.");

            if (!_hasher.Verify(req.CurrentPassword, user.PasswordHash))
                throw new UnauthorizedAppException("Current password is incorrect.");

            if (_hasher.Verify(req.NewPassword, user.PasswordHash))
                throw new BadRequestAppException("New password must be different from the current password.");

            user.PasswordHash = _hasher.Hash(req.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            _auditService.Log(userId, "AUTH.PASSWORD_CHANGED", ip, true);
            await _db.SaveChangesAsync();

            return new MessageResponse { Message = "Password changed successfully." };
        }
    }
}
