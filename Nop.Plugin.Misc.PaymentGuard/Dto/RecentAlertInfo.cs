namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public record RecentAlertInfo
    {
        public int Id { get; set; }
        public string AlertType { get; set; }
        public string AlertLevel { get; set; }
        public string Message { get; set; }
        public string ScriptUrl { get; set; }
        public string TimeAgo { get; set; }
        public bool IsResolved { get; set; }
        public string BadgeClass { get; set; }
    }
}