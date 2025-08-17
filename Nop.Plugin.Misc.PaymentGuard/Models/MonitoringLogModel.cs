using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record MonitoringLogModel : BaseNopEntityModel
    {
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.PageUrl")]
        public string PageUrl { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.TotalScriptsFound")]
        public int TotalScriptsFound { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.AuthorizedScriptsCount")]
        public int AuthorizedScriptsCount { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.UnauthorizedScriptsCount")]
        public int UnauthorizedScriptsCount { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.HasUnauthorizedScripts")]
        public bool HasUnauthorizedScripts { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.CheckedOnUtc")]
        public DateTime CheckedOnUtc { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.CheckType")]
        public string CheckType { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.AlertSent")]
        public bool AlertSent { get; set; }

        public IList<string> DetectedScripts { get; set; } = new List<string>();
        public IList<string> UnauthorizedScripts { get; set; } = new List<string>();
        public IDictionary<string, string> SecurityHeaders { get; set; } = new Dictionary<string, string>();

        public string StatusClass => HasUnauthorizedScripts ? "danger" : "success";
        public string StatusText => HasUnauthorizedScripts ? "Issues Found" : "Compliant";
    }
}