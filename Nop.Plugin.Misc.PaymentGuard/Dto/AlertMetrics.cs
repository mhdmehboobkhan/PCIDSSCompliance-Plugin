namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public record AlertMetrics
    {
        public int ActiveAlertsCount { get; set; }
        public int CriticalAlertsCount { get; set; }
        public int NewAlertsToday { get; set; }
        public double ResolutionRate { get; set; }
        public double AverageResolutionTimeHours { get; set; }
    }
}