namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public record RealTimeMetrics
    {
        public string SystemStatus { get; set; }
        public int AlertsLastHour { get; set; }
        public int AlertsLast24Hours { get; set; }
        public DateTime? LastAlertTime { get; set; }
        public int ActiveMonitoringSessions { get; set; }
    }
}