using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Infrastructure.options
{
    public class AiOptions
    {
        public string PlannerUrl { get; set; } = string.Empty;
        public string ChatUrl { get; set; } = string.Empty;
        public string EditUrl { get; set; } = string.Empty;
        public string RecommendBase { get; set; } = string.Empty;
    }
}
