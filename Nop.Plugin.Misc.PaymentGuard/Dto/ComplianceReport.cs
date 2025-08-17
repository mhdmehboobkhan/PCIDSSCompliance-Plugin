namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class ComplianceReport
    {
        public int TotalScriptsMonitored { get; set; }
        public int AuthorizedScriptsCount { get; set; }
        public int UnauthorizedScriptsCount { get; set; }
        public int TotalChecksPerformed { get; set; }
        public int AlertsGenerated { get; set; }
        public DateTime LastCheckDate { get; set; }
        public IList<string> MostCommonUnauthorizedScripts { get; set; } = new List<string>();
        public IList<string> RecentAlerts { get; set; } = new List<string>();
        public double ComplianceScore { get; set; }
    }
}