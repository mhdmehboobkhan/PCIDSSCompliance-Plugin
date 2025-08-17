namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class PaymentMethodMonitoringRequest
    {
        public string SessionId { get; set; }
        public string PageUrl { get; set; }
        public string PaymentMethod { get; set; }
        public List<string> PaymentScripts { get; set; } = new();
        public string Context { get; set; } // payment-focus, payment-selection, etc.
        public string UserAgent { get; set; }
        public string Timestamp { get; set; }
    }
}