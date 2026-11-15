using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Application.Interfaces
{
    public interface IOtpService
    {
        string GenerateOtp();
        string Hash(string otp);
        bool Verify(string otp, string hashedOtp);
    }
}
