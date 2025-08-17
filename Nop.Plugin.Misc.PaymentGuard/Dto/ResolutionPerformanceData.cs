namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public record ResolutionPerformanceData
    {
        public DateTime Date { get; set; }
        public int AlertsResolved { get; set; }
        public double AverageResolutionTimeHours { get; set; }
    }
}