using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.Misc.PaymentGuard.Dto;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record DashboardModel : BaseNopModel
    {
        public int SearchStoreId { get; set; }

        public int TotalScriptsMonitored { get; set; }
        public int AuthorizedScriptsCount { get; set; }
        public int UnauthorizedScriptsCount { get; set; }
        public int ExpiredScriptsCount { get; set; }
        public double ComplianceScore { get; set; }
        public DateTime LastCheckDate { get; set; }
        public int TotalChecksPerformed { get; set; }
        public int AlertsGenerated { get; set; }
        public int SelectedDays { get; set; }

        public IList<string> MostCommonUnauthorizedScripts { get; set; } = new List<string>();
        public IList<SelectListItem> AvailableDayOptions { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();

        public int ActiveAlertsCount { get; set; }
        public int CriticalAlertsCount { get; set; }
        public int NewAlertsToday { get; set; }
        public double AlertResolutionRate { get; set; }
        public double AverageResolutionTimeHours { get; set; }

        public IList<ComplianceChartDataPoint> ComplianceHistoryData { get; set; } = new List<ComplianceChartDataPoint>();
        public IList<AlertTypeChartData> AlertTypeDistribution { get; set; } = new List<AlertTypeChartData>();
        public IList<MonitoringTrendData> MonitoringTrends { get; set; } = new List<MonitoringTrendData>();
        public IList<RiskLevelData> RiskLevelBreakdown { get; set; } = new List<RiskLevelData>();
        public IList<TopViolatingScriptsData> TopViolatingScripts { get; set; } = new List<TopViolatingScriptsData>();

        public ComplianceMetrics ComplianceMetrics { get; set; } = new ComplianceMetrics();
        public PerformanceMetrics PerformanceMetrics { get; set; } = new PerformanceMetrics();
        public IList<ExpiredScriptInfo> ExpiredScripts { get; set; } = new List<ExpiredScriptInfo>();

        public IList<RecentAlertInfo> RecentAlerts { get; set; } = new List<RecentAlertInfo>();
        public IList<AlertTrendData> AlertTrends { get; set; } = new List<AlertTrendData>();
        public IList<ResolutionPerformanceData> ResolutionPerformance { get; set; } = new List<ResolutionPerformanceData>();
        public RealTimeMetrics RealTimeMetrics { get; set; } = new RealTimeMetrics();

        // Helper properties
        public string SystemStatusBadgeClass => RealTimeMetrics.SystemStatus switch
        {
            "critical" => "badge-danger",
            "warning" => "badge-warning",
            "healthy" => "badge-success",
            _ => "badge-secondary"
        };

        public string SystemStatusText => RealTimeMetrics.SystemStatus switch
        {
            "critical" => "Critical Issues",
            "warning" => "Warnings Detected",
            "healthy" => "System Healthy",
            _ => "Unknown"
        };

        public bool HasCriticalIssues => CriticalAlertsCount > 0;
        public bool HasActiveAlerts => ActiveAlertsCount > 0;
        public string LastAlertTimeText => RealTimeMetrics.LastAlertTime?.ToString("MMM dd, HH:mm") ?? "No recent alerts";
    }

    public record ComplianceChartDataPoint
    {
        public DateTime Date { get; set; }
        public double ComplianceScore { get; set; }
        public int TotalScripts { get; set; }
        public int AuthorizedScripts { get; set; }
        public int UnauthorizedScripts { get; set; }
    }

    public record AlertTypeChartData
    {
        public string AlertType { get; set; }
        public int Count { get; set; }
        public string Color { get; set; }
        public double Percentage { get; set; }
    }

    public record MonitoringTrendData
    {
        public DateTime Date { get; set; }
        public int ChecksPerformed { get; set; }
        public int IssuesFound { get; set; }
        public double AverageResponseTime { get; set; }
    }

    public record RiskLevelData
    {
        public string RiskLevel { get; set; }
        public int Count { get; set; }
        public string Color { get; set; }
        public double Percentage { get; set; }
    }

    public record TopViolatingScriptsData
    {
        public string ScriptUrl { get; set; }
        public int ViolationCount { get; set; }
        public string LastViolation { get; set; }
        public int RiskLevelId { get; set; }
        public string RiskLevelText { get; set; }
    }

    public record ComplianceMetrics
    {
        public double ComplianceImprovement { get; set; }
        public int ResolvedAlertsThisWeek { get; set; }
        public int NewAlertsThisWeek { get; set; }
        public double AverageResolutionTime { get; set; } // in hours
        public int ScriptsAddedThisWeek { get; set; }
        public double SecurityPosture { get; set; } // 0-100 score
    }

    public record PerformanceMetrics
    {
        public double AverageMonitoringTime { get; set; } // in seconds
        public int SuccessfulChecks { get; set; }
        public int FailedChecks { get; set; }
        public double SystemUptime { get; set; } // percentage
        public int ApiCallsThisWeek { get; set; }
        public double CacheHitRate { get; set; } // percentage
    }

    public record ExpiredScriptInfo
    {
        public string ScriptUrl { get; set; }
        public DateTime LastVerified { get; set; }
        public int DaysExpired { get; set; }
    }
}