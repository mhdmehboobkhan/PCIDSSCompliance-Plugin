namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class ReportScriptsRequest
    {
        public List<string> Scripts { get; set; } = new();
        public string PageUrl { get; set; }
        public string UserAgent { get; set; }
        public string Timestamp { get; set; }
        public List<string> InitialScripts { get; set; } = new();
    }
}