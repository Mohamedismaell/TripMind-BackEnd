using System.Threading.Tasks;

namespace TripMind.Application.Interfaces
{
    public interface IEmailSender
    {
        Task SendPasswordResetOtpAsync(string toEmail, string displayName, string otp);

        Task SendEmailVerificationOtpAsync(string toEmail, string displayName, string otp);

        Task SendTwoFactorOtpAsync(string toEmail, string displayName, string otp);
    }
}
