using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TripMind.Application.Interfaces;

namespace TripMind.Application.Services
{
    public sealed class OtpService : IOtpService
    {
        private readonly IPasswordHasher _hasher;
        public OtpService(IPasswordHasher hasher)
        {
            _hasher = hasher;
        }
        public string GenerateOtp()
        {
            return RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        }

        public string Hash(string otp)
        {
            return _hasher.Hash(otp);
        }

        public bool Verify(string otp, string hashedOtp)
        {
            return _hasher.Verify(otp, hashedOtp);
        }
    }
}
