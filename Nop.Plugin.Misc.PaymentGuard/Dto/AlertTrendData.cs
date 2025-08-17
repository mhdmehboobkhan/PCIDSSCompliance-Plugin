namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public record AlertTrendData
    {
        public DateTime Date { get; set; }
        public int TotalAlerts { get; set; }
        public int CriticalAlerts { get; set; }
        public int WarningAlerts { get; set; }
        public int InfoAlerts { get; set; }
        public int ResolvedAlerts { get; set; }
    }
}