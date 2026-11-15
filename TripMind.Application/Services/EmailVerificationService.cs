using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;
using TripMind.Application.Exceptions;
using TripMind.Application.Interfaces;

namespace TripMind.Application.Services
{
    public sealed class EmailVerificationService : IEmailVerificationService
    {
        private readonly IAppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly IEmailSender _email;
        private readonly IOtpService _otpService;
        private readonly IAuditService _auditService;

        public EmailVerificationService(
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

        public async Task<MessageResponse> VerifyEmailAsync(VerifyEmailOtpRequest req, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant().Trim())
                ?? throw new BadRequestAppException("Invalid request.");

            if (user.IsEmailVerified)
                return new MessageResponse { Message = "Email is already verified." };

            if (user.EmailOtpExpiry == null || user.EmailOtpExpiry < DateTime.UtcNow)
                throw new BadRequestAppException("OTP has expired. Please request a new one.");

            if (user.EmailVerificationOtp == null || !_hasher.Verify(req.Otp, user.EmailVerificationOtp))
                throw new BadRequestAppException("Invalid OTP.");

            user.IsEmailVerified = true;
            user.EmailVerificationOtp = null;
            user.EmailOtpExpiry = null;
            user.UpdatedAt = DateTime.UtcNow;

            _auditService.Log(user.UserId, "AUTH.EMAIL_VERIFIED", ip, true);
            await _db.SaveChangesAsync();

            return new MessageResponse { Message = "Email verified successfully. You can now login." };
        }

        public async Task<MessageResponse> ResendEmailOtpAsync(ResendEmailOtpRequest req, string? ip = null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant().Trim());
            if (user == null || user.IsEmailVerified) { await Task.Delay(200); return new MessageResponse { Message = "If that email is registered and unverified, a new OTP has been sent." }; }

            string otp = _otpService.GenerateOtp();
            user.EmailVerificationOtp = _hasher.Hash(otp);
            user.EmailOtpExpiry = DateTime.UtcNow.AddMinutes(15);
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _email.SendEmailVerificationOtpAsync(user.Email, user.DisplayName, otp);

            return new MessageResponse { Message = "If that email is registered and unverified, a new OTP has been sent." };
        }
    }
}
