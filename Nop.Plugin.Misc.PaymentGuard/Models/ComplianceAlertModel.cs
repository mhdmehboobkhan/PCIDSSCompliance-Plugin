using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record ComplianceAlertModel : BaseNopEntityModel
    {
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.AlertType")]
        public string AlertType { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.AlertLevel")]
        public string AlertLevel { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.Message")]
        public string Message { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.Details")]
        public string Details { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.ScriptUrl")]
        public string ScriptUrl { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.PageUrl")]
        public string PageUrl { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.IsResolved")]
        public bool IsResolved { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.CreatedOnUtc")]
        public DateTime CreatedOnUtc { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.ResolvedOnUtc")]
        public DateTime? ResolvedOnUtc { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.ResolvedBy")]
        public string ResolvedBy { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.EmailSent")]
        public bool EmailSent { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.EmailSentOnUtc")]
        public DateTime? EmailSentOnUtc { get; set; }

        // Helper properties for display
        public string AlertLevelBadgeClass => AlertLevel switch
        {
            "critical" => "badge-danger",
            "warning" => "badge-warning",
            "info" => "badge-info",
            _ => "badge-secondary"
        };

        public string StatusBadgeClass => IsResolved ? "badge-success" : "badge-warning";
        public string StatusText => IsResolved ? "Resolved" : "Unresolved";
    }
}