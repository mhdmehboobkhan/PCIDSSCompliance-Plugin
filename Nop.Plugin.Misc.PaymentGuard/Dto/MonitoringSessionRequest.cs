namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class MonitoringSessionRequest
    {
        public string SessionId { get; set; }
        public string PageUrl { get; set; }
        public List<string> DetectedScripts { get; set; } = new();
        public List<string> PaymentScripts { get; set; } = new();
        public Dictionary<string, string> Headers { get; set; }
        public string UserAgent { get; set; }
        public string Context { get; set; }
        public string CheckType { get; set; }
        public string Timestamp { get; set; }
    }
}