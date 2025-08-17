using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Misc.PaymentGuard.Domain
{
    public partial class ComplianceAlert : BaseEntity
    {
        public int StoreId { get; set; }

        public string AlertType { get; set; } // "unauthorized-script", "integrity-failure", "csp-violation"

        public string AlertLevel { get; set; } // "info", "warning", "critical"

        public string Message { get; set; }

        public string Details { get; set; } // JSON with additional data

        public string ScriptUrl { get; set; }

        public string PageUrl { get; set; }

        public bool IsResolved { get; set; }

        public DateTime CreatedOnUtc { get; set; }

        public DateTime? ResolvedOnUtc { get; set; }

        public string ResolvedBy { get; set; }

        public bool EmailSent { get; set; }

        public DateTime? EmailSentOnUtc { get; set; }
    }
}