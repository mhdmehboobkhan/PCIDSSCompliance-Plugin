using Nop.Plugin.Misc.PaymentGuard.Controllers;

namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class ReportCSPViolationRequest
    {
        public CSPViolationData Violation { get; set; }
        public string PageUrl { get; set; }
        public string Timestamp { get; set; }
        public string UserAgent { get; set; }
    }
}