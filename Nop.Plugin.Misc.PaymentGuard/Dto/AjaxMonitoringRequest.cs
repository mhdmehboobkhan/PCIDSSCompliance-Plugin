namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class AjaxMonitoringRequest
    {
        public string SessionId { get; set; }
        public string PageUrl { get; set; }
        public List<string> NewScripts { get; set; } = new();
        public List<string> PreAjaxScripts { get; set; } = new();
        public string AjaxSource { get; set; } // xhr, fetch, jquery
        public string Context { get; set; }
        public string UserAgent { get; set; }
        public string Timestamp { get; set; }
    }
}