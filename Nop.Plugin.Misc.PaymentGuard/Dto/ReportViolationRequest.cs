namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class ReportViolationRequest
    {
        public string ViolationType { get; set; }
        public string ScriptUrl { get; set; }
        public string PageUrl { get; set; }
        public string Timestamp { get; set; }
        public string UserAgent { get; set; }
    }
}