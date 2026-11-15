using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TripMind.Application.Interfaces;
using TripMind.Infrastructure.options;

namespace TripMind.Infrastructure.Services
{
    public sealed class EmailDomainPolicy : IEmailDomainPolicy
    {
        private readonly EmailPolicyOptions _options;

        public EmailDomainPolicy(IOptions<EmailPolicyOptions> options)
        {
            _options = options.Value;
        }
        public bool IsBlocked(string email)
        {
            var emailDomain = email.Split('@')[1].ToLower();
            return _options.BlockedDomains.Contains(emailDomain, StringComparer.OrdinalIgnoreCase);
        }
    }
}
