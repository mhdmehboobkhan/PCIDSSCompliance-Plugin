using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Misc.PaymentGuard.Domain
{
    public partial class ScriptMonitoringLog : BaseEntity
    {
        public int StoreId { get; set; }

        public string PageUrl { get; set; }

        public string DetectedScripts { get; set; } // JSON array of scripts found

        public string HttpHeaders { get; set; } // JSON object of security headers

        public bool HasUnauthorizedScripts { get; set; }

        public string UnauthorizedScripts { get; set; } // JSON array

        public bool AlertSent { get; set; }

        public DateTime CheckedOnUtc { get; set; }

        public string CheckType { get; set; } // "scheduled", "manual", "real-time"

        public string UserAgent { get; set; }

        public int TotalScriptsFound { get; set; }

        public int AuthorizedScriptsCount { get; set; }

        public int UnauthorizedScriptsCount { get; set; }
    }
}