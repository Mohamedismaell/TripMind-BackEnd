using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Infrastructure.options
{
    public sealed class EmailPolicyOptions
    {
        public string[] BlockedDomains { get; set; } = [];
    }
}
