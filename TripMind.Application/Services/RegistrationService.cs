using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TripMind.Application.DTOs.Auth;
using TripMind.Application.Exceptions;
using TripMind.Application.Interfaces;
using TripMind.Domain.Entities;

namespace TripMind.Application.Services
{
    public sealed class RegistrationService : IRegistrationService
    {
        private readonly IAppDbContext _db;
        private readonly IEmailDomainPolicy _emailDomainPolicy;
        private readonly IPasswordHasher _hasher;
        private readonly IEmailSender _email;
        private readonly IOtpService _otpService;
        private readonly IAuditService _auditService;
        public RegistrationService(IAppDbContext db, IEmailDomainPolicy emailDomainPolicy, IOtpService otpService, IPasswordHasher hasher, IEmailSender email, IAuditService auditService)
        {
            _db = db;
            _emailDomainPolicy = emailDomainPolicy;
            _otpService = otpService;
            _hasher = hasher;
            _email = email;
            _auditService = auditService;
        }
        public async Task<MessageResponse> RegisterAsync(RegisterRequest req, string? ip = null)
        {


            var email = req.Email.Trim().ToLowerInvariant();
            if (await _db.Users.AnyAsync(u => u.Email == email))
                throw new ConflictAppException("An account with this email already exists.");

            if (_emailDomainPolicy.IsBlocked(email))
            {
                throw new BadRequestAppException(
                    "Please use a valid email address.");
            }

            var now = DateTime.UtcNow;
            string otp = _otpService.GenerateOtp();


            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = email,
                DisplayName = req.DisplayName.Trim(),
                PasswordHash = _hasher.Hash(req.Password),
                RememberMe = req.RememberMe,
                IsActive = true,
                IsEmailVerified = false,
                EmailVerificationOtp = _hasher.Hash(otp),
                EmailOtpExpiry = now.AddMinutes(15),
                LanguagePreference = "AR",
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _auditService.Log(user.UserId, "AUTH.REGISTER", ip, true);
            await _db.SaveChangesAsync();

            try
            {
                await _email.SendEmailVerificationOtpAsync(user.Email, user.DisplayName, otp);
            }
            catch
            {
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
                throw new ServiceUnavailableAppException("Failed to send verification email. Please try again.");
            }

            return new MessageResponse { Message = "Registration successful. Please check your email to verify your account." };

        }

    }
}
