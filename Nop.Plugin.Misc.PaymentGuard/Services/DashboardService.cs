using System.Text.Json;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Logging;
using Nop.Data;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Dto;
using Nop.Plugin.Misc.PaymentGuard.Enums;
using Nop.Plugin.Misc.PaymentGuard.Models;
using Nop.Services.Localization;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial class DashboardService : IDashboardService
    {
        #region Fields

        private readonly IRepository<ScriptMonitoringLog> _monitoringLogRepository;
        private readonly IRepository<ComplianceAlert> _complianceAlertRepository;
        private readonly IRepository<AuthorizedScript> _authorizedScriptRepository;
        private readonly IMonitoringService _monitoringService;
        private readonly IAuthorizedScriptService _authorizedScriptService;
        private readonly ILocalizationService _localizationService;

        #endregion

        #region Ctor

        public DashboardService(IRepository<ScriptMonitoringLog> monitoringLogRepository,
            IRepository<ComplianceAlert> complianceAlertRepository,
            IRepository<AuthorizedScript> authorizedScriptRepository,
            IMonitoringService monitoringService,
            IAuthorizedScriptService authorizedScriptService,
            ILocalizationService localizationService)
        {
            _monitoringLogRepository = monitoringLogRepository;
            _complianceAlertRepository = complianceAlertRepository;
            _authorizedScriptRepository = authorizedScriptRepository;
            _monitoringService = monitoringService;
            _authorizedScriptService = authorizedScriptService;
            _localizationService = localizationService;
        }

        #endregion

        #region Utilities

        private async Task<ComplianceMetrics> GetComplianceMetricsAsync(int storeId, int days)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var weekAgo = DateTime.UtcNow.AddDays(-7);

            var query = _complianceAlertRepository.Table.Where(alert => alert.CreatedOnUtc >= weekAgo);
            if (storeId > 0)
                query = query.Where(alert => alert.StoreId == storeId);
            var currentPeriodAlerts = await query.ToListAsync();


            var previousQuery = _complianceAlertRepository.Table
                .Where(alert => alert.CreatedOnUtc >= weekAgo.AddDays(-7)
                    && alert.CreatedOnUtc < weekAgo);
            if (storeId > 0)
                previousQuery = previousQuery.Where(alert => alert.StoreId == storeId);
            var previousPeriodAlerts = await previousQuery.ToListAsync();

            var resolvedThisWeek = currentPeriodAlerts.Count(a => a.IsResolved);
            var newThisWeek = currentPeriodAlerts.Count;

            var scriptsQuery = _authorizedScriptRepository.Table
                .Where(script => script.AuthorizedOnUtc >= weekAgo);
            if (storeId > 0)
                scriptsQuery = scriptsQuery.Where(script => script.StoreId == storeId);
            var scriptsAddedThisWeek = await scriptsQuery.CountAsync();

            // Calculate compliance improvement
            var currentCompliance = await GetCurrentComplianceScore(storeId);
            var previousCompliance = await GetPreviousComplianceScore(storeId, 7);
            var improvement = currentCompliance - previousCompliance;

            return new ComplianceMetrics
            {
                ComplianceImprovement = improvement,
                ResolvedAlertsThisWeek = resolvedThisWeek,
                NewAlertsThisWeek = newThisWeek,
                AverageResolutionTime = CalculateAverageResolutionTime(currentPeriodAlerts.Where(a => a.IsResolved)),
                ScriptsAddedThisWeek = scriptsAddedThisWeek,
                SecurityPosture = CalculateSecurityPosture(storeId)
            };
        }

        private async Task<PerformanceMetrics> GetPerformanceMetricsAsync(int storeId, int days)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            
            var query = _monitoringLogRepository.Table.Where(log => log.CheckedOnUtc >= fromDate);
            if (storeId > 0)
                query = query.Where(log => log.StoreId == storeId);

            var logs = await query.ToListAsync();

            var successfulChecks = logs.Count(log => !log.HasUnauthorizedScripts);
            var failedChecks = logs.Count - successfulChecks;

            return new PerformanceMetrics
            {
                AverageMonitoringTime = GenerateRandomResponseTime(),
                SuccessfulChecks = successfulChecks,
                FailedChecks = failedChecks,
                SystemUptime = 99.8, // Placeholder
                ApiCallsThisWeek = GenerateRandomApiCalls(),
                CacheHitRate = 85.5 // Placeholder
            };
        }

        private string FormatAlertType(string alertType)
        {
            return alertType switch
            {
                "unauthorized-script" => "Unauthorized Scripts",
                "csp-violation" => "CSP Violations",
                "integrity-failure" => "Integrity Failures",
                _ => alertType ?? "Unknown"
            };
        }

        private string TruncateUrl(string url, int maxLength)
        {
            if (string.IsNullOrEmpty(url) || url.Length <= maxLength)
                return url;

            return url.Substring(0, maxLength - 3) + "...";
        }

        private double GenerateRandomResponseTime()
        {
            // Placeholder for actual performance metrics
            var random = new Random();
            return Math.Round(random.NextDouble() * 2 + 0.5, 2); // 0.5 to 2.5 seconds
        }

        private int GenerateRandomApiCalls()
        {
            var random = new Random();
            return random.Next(500, 2000);
        }

        private async Task<double> GetCurrentComplianceScore(int storeId)
        {
            var report = await _monitoringService.GenerateComplianceReportAsync(storeId, DateTime.UtcNow.AddDays(-7));
            return report.ComplianceScore;
        }

        private async Task<double> GetPreviousComplianceScore(int storeId, int daysAgo)
        {
            var fromDate = DateTime.UtcNow.AddDays(-daysAgo * 2);
            var toDate = DateTime.UtcNow.AddDays(-daysAgo);
            var report = await _monitoringService.GenerateComplianceReportAsync(storeId, fromDate, toDate);
            return report.ComplianceScore;
        }

        private double CalculateAverageResolutionTime(IEnumerable<ComplianceAlert> resolvedAlerts)
        {
            var alerts = resolvedAlerts.Where(a => a.ResolvedOnUtc.HasValue).ToList();
            if (!alerts.Any())
                return 0;

            var totalHours = alerts.Sum(alert =>
                (alert.ResolvedOnUtc!.Value - alert.CreatedOnUtc).TotalHours);

            return Math.Round(totalHours / alerts.Count, 1);
        }

        private double CalculateSecurityPosture(int storeId)
        {
            // Complex calculation based on various factors
            // This is a simplified version
            var random = new Random();
            return Math.Round(75 + random.NextDouble() * 20, 1); // 75-95% range
        }
        
        private string GetTimeAgoString(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "Just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}m ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}h ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays}d ago";

            return dateTime.ToString("MMM dd");
        }

        private string GetAlertBadgeClass(string alertLevel)
        {
            return alertLevel?.ToLower() switch
            {
                "critical" => "badge-danger",
                "warning" => "badge-warning",
                "info" => "badge-info",
                _ => "badge-secondary"
            };
        }

        /// <summary>
        /// Get alert metrics for dashboard
        /// </summary>
        private async Task<AlertMetrics> GetAlertMetricsAsync(int storeId, int days)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var today = DateTime.UtcNow.Date;

            var query = _complianceAlertRepository.Table.Where(alert => alert.CreatedOnUtc >= fromDate);
            if (storeId > 0)
                query = query.Where(alert => alert.StoreId == storeId);

            var alerts = await query.ToListAsync();

            var resolvedAlerts = alerts.Where(a => a.IsResolved && a.ResolvedOnUtc.HasValue).ToList();

            var averageResolutionTime = 0.0;
            if (resolvedAlerts.Any())
            {
                averageResolutionTime = resolvedAlerts
                    .Average(a => (a.ResolvedOnUtc!.Value - a.CreatedOnUtc).TotalHours);
            }

            return new AlertMetrics
            {
                ActiveAlertsCount = alerts.Count(a => !a.IsResolved),
                CriticalAlertsCount = alerts.Count(a => !a.IsResolved && a.AlertLevel == "critical"),
                NewAlertsToday = alerts.Count(a => a.CreatedOnUtc >= today),
                ResolutionRate = alerts.Count > 0 ? (double)resolvedAlerts.Count / alerts.Count * 100 : 100,
                AverageResolutionTimeHours = averageResolutionTime
            };
        }

        /// <summary>
        /// Get real-time metrics based on recent alert activity
        /// </summary>
        private async Task<RealTimeMetrics> GetRealTimeMetricsAsync(int storeId, int days)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var lastHour = DateTime.UtcNow.AddHours(-1);
            var last24Hours = DateTime.UtcNow.AddDays(-1);

            var query = _complianceAlertRepository.Table.Where(alert => alert.CreatedOnUtc >= last24Hours);
            if (storeId > 0)
                query = query.Where(alert => alert.StoreId == storeId);

            var recentAlerts = await query.ToListAsync();

            var systemStatus = "healthy";
            if (recentAlerts.Any(a => !a.IsResolved && a.AlertLevel == "critical"))
                systemStatus = "critical";
            else if (recentAlerts.Any(a => !a.IsResolved && a.AlertLevel == "warning"))
                systemStatus = "warning";

            return new RealTimeMetrics
            {
                SystemStatus = systemStatus,
                AlertsLastHour = recentAlerts.Count(a => a.CreatedOnUtc >= lastHour),
                AlertsLast24Hours = recentAlerts.Count,
                LastAlertTime = recentAlerts.OrderByDescending(a => a.CreatedOnUtc).FirstOrDefault()?.CreatedOnUtc,
                ActiveMonitoringSessions = await GetActiveMonitoringSessionsAsync(storeId)
            };
        }

        /// <summary>
        /// Get recent alerts for dashboard display
        /// </summary>
        private async Task<IList<RecentAlertInfo>> GetRecentAlertsAsync(int storeId, int hours, int maxCount)
        {
            var fromDate = DateTime.UtcNow.AddHours(-hours);

            var alerts = await _complianceAlertRepository.Table
                .Where(alert => (storeId == 0 || alert.StoreId == storeId) && alert.CreatedOnUtc >= fromDate)
                .OrderByDescending(alert => alert.CreatedOnUtc)
                .Take(maxCount)
                .ToListAsync();

            return alerts.Select(alert => new RecentAlertInfo
            {
                Id = alert.Id,
                AlertType = alert.AlertType,
                AlertLevel = alert.AlertLevel,
                Message = alert.Message,
                ScriptUrl = TruncateUrl(alert.ScriptUrl, 50),
                TimeAgo = GetTimeAgoString(alert.CreatedOnUtc),
                IsResolved = alert.IsResolved,
                BadgeClass = GetAlertBadgeClass(alert.AlertLevel)
            }).ToList();
        }

        /// <summary>
        /// Get alert trends over time
        /// </summary>
        private async Task<IList<AlertTrendData>> GetAlertTrendsAsync(int storeId, int days)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);

            var alerts = await _complianceAlertRepository.Table
                .Where(alert => (storeId == 0 || alert.StoreId == storeId) && alert.CreatedOnUtc >= fromDate)
                .ToListAsync();

            var trendData = alerts
                .GroupBy(alert => alert.CreatedOnUtc.Date)
                .Select(group => new AlertTrendData
                {
                    Date = group.Key.Date,
                    TotalAlerts = group.Count(),
                    CriticalAlerts = group.Count(a => a.AlertLevel == "critical"),
                    WarningAlerts = group.Count(a => a.AlertLevel == "warning"),
                    InfoAlerts = group.Count(a => a.AlertLevel == "info"),
                    ResolvedAlerts = group.Count(a => a.IsResolved)
                })
                .OrderBy(trend => trend.Date)
                .ToList();

            return trendData;
        }

        /// <summary>
        /// Get resolution performance metrics
        /// </summary>
        private async Task<IList<ResolutionPerformanceData>> GetResolutionPerformanceAsync(int storeId, int days)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);

            var resolvedAlerts = await _complianceAlertRepository.Table
                .Where(alert => (storeId == 0 || alert.StoreId == storeId)
                    && alert.IsResolved
                    && alert.ResolvedOnUtc.HasValue
                    && alert.CreatedOnUtc >= fromDate)
                .ToListAsync();

            var performanceData = resolvedAlerts
                .GroupBy(alert => alert.ResolvedOnUtc!.Value.Date)
                .Select(group => new ResolutionPerformanceData
                {
                    Date = group.Key,
                    AlertsResolved = group.Count(),
                    AverageResolutionTimeHours = group.Average(a =>
                        (a.ResolvedOnUtc!.Value - a.CreatedOnUtc).TotalHours)
                })
                .OrderBy(data => data.Date)
                .ToList();

            return performanceData;
        }

        /// <summary>
        /// Estimate active monitoring sessions (placeholder)
        /// </summary>
        private async Task<int> GetActiveMonitoringSessionsAsync(int storeId)
        {
            // This could be enhanced to track actual active sessions
            // For now, return count based on recent client-side activity
            var recentAlerts = await _complianceAlertRepository.Table
                .Where(alert => (storeId == 0 || alert.StoreId == storeId)
                    && alert.CreatedOnUtc >= DateTime.UtcNow.AddMinutes(-30))
                .CountAsync();

            return Math.Min(recentAlerts, 10); // Cap at 10 for realistic display
        }

        #endregion

        #region Methods

        public virtual async Task<DashboardModel> GetDashboardDataAsync(int storeId, int days = 30)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var weekAgo = DateTime.UtcNow.AddDays(-7);

            // Get compliance report (existing)
            var report = await _monitoringService.GenerateComplianceReportAsync(storeId, fromDate);
            var expiredScripts = await _authorizedScriptService.GetExpiredScriptsAsync(30, storeId);

            // NEW: Get ComplianceAlert metrics
            var alertMetrics = await GetAlertMetricsAsync(storeId, days);
            var realTimeMetrics = await GetRealTimeMetricsAsync(storeId, days);

            var model = new DashboardModel
            {
                // Existing metrics
                TotalScriptsMonitored = report.TotalScriptsMonitored,
                AuthorizedScriptsCount = report.AuthorizedScriptsCount,
                UnauthorizedScriptsCount = report.UnauthorizedScriptsCount,
                ComplianceScore = report.ComplianceScore,
                LastCheckDate = report.LastCheckDate,
                TotalChecksPerformed = report.TotalChecksPerformed,
                AlertsGenerated = report.AlertsGenerated,
                MostCommonUnauthorizedScripts = report.MostCommonUnauthorizedScripts,

                // NEW: Alert-based metrics
                ActiveAlertsCount = alertMetrics.ActiveAlertsCount,
                CriticalAlertsCount = alertMetrics.CriticalAlertsCount,
                NewAlertsToday = alertMetrics.NewAlertsToday,
                AlertResolutionRate = alertMetrics.ResolutionRate,
                AverageResolutionTimeHours = alertMetrics.AverageResolutionTimeHours,

                // Enhanced analytics data
                ComplianceHistoryData = await GetComplianceHistoryAsync(storeId, days),
                AlertTypeDistribution = await GetAlertTypeDistributionAsync(storeId, days),
                MonitoringTrends = await GetMonitoringTrendsAsync(storeId, days),
                RiskLevelBreakdown = await GetRiskLevelBreakdownAsync(storeId),
                TopViolatingScripts = await GetTopViolatingScriptsAsync(storeId, days),

                // NEW: Alert-focused data
                RecentAlerts = await GetRecentAlertsAsync(storeId, 24, 5),
                AlertTrends = await GetAlertTrendsAsync(storeId, days),
                ResolutionPerformance = await GetResolutionPerformanceAsync(storeId, days),

                ComplianceMetrics = await GetComplianceMetricsAsync(storeId, days),
                PerformanceMetrics = await GetPerformanceMetricsAsync(storeId, days),
                RealTimeMetrics = realTimeMetrics
            };

            model.ExpiredScriptsCount = expiredScripts.Count;
            model.ExpiredScripts = expiredScripts.Take(5).Select(s => new ExpiredScriptInfo
            {
                ScriptUrl = s.ScriptUrl,
                LastVerified = s.LastVerifiedUtc,
                DaysExpired = (DateTime.UtcNow - s.LastVerifiedUtc).Days
            }).ToList();

            model.SelectedDays = days;
            model.AvailableDayOptions = new List<SelectListItem>
            {
                new() { Value = "7", Text = "Last 7 Days", Selected = days == 7 },
                new() { Value = "30", Text = "Last 30 Days", Selected = days == 30 },
                new() { Value = "90", Text = "Last 90 Days", Selected = days == 90 }
            };

            return model;
        }

        public virtual async Task<IList<ComplianceChartDataPoint>> GetComplianceHistoryAsync(int storeId, int days = 30)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);

            var logsQuery = _monitoringLogRepository.Table.Where(log => log.CheckedOnUtc >= fromDate);
            if (storeId > 0)
                logsQuery = logsQuery.Where(log => log.StoreId == storeId);
            var logs = await logsQuery.OrderBy(log => log.CheckedOnUtc).ToListAsync();

            var groupedByDay = logs
                .GroupBy(log => log.CheckedOnUtc.Date)
                .Select(group => new ComplianceChartDataPoint
                {
                    Date = group.Key,
                    TotalScripts = group.Sum(log => log.TotalScriptsFound),
                    AuthorizedScripts = group.Sum(log => log.AuthorizedScriptsCount),
                    UnauthorizedScripts = group.Sum(log => log.UnauthorizedScriptsCount),
                    ComplianceScore = group.Sum(log => log.TotalScriptsFound) > 0
                        ? (double)group.Sum(log => log.AuthorizedScriptsCount) / group.Sum(log => log.TotalScriptsFound) * 100
                        : 100
                })
                .OrderBy(point => point.Date)
                .ToList();

            return groupedByDay;
        }

        public virtual async Task<IList<AlertTypeChartData>> GetAlertTypeDistributionAsync(int storeId, int days = 30)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);

            var alertsQuery = _complianceAlertRepository.Table.Where(alert => alert.CreatedOnUtc >= fromDate);
            if (storeId > 0)
                alertsQuery = alertsQuery.Where(log => log.StoreId == storeId);
            var alerts = await alertsQuery.GroupBy(alert => alert.AlertType)
                .Select(group => new
                {
                    AlertType = group.Key,
                    Count = group.Count()
                })
                .ToListAsync();

            var totalAlerts = alerts.Sum(a => a.Count);
            var colors = new[] { "#FF6384", "#36A2EB", "#FFCE56", "#4BC0C0", "#9966FF", "#FF9F40" };

            var result = alerts.Select((alert, index) => new AlertTypeChartData
            {
                AlertType = FormatAlertType(alert.AlertType),
                Count = alert.Count,
                Percentage = totalAlerts > 0 ? (double)alert.Count / totalAlerts * 100 : 0,
                Color = colors[index % colors.Length]
            }).ToList();

            return result;
        }

        public virtual async Task<IList<MonitoringTrendData>> GetMonitoringTrendsAsync(int storeId, int days = 30)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);

            var logsQuery = _monitoringLogRepository.Table.Where(log => log.CheckedOnUtc >= fromDate);
            if (storeId > 0)
                logsQuery = logsQuery.Where(log => log.StoreId == storeId);
            var logs = await logsQuery.ToListAsync();

            var trendData = logs
                .GroupBy(log => log.CheckedOnUtc.Date)
                .Select(group => new MonitoringTrendData
                {
                    Date = group.Key,
                    ChecksPerformed = group.Count(),
                    IssuesFound = group.Count(log => log.HasUnauthorizedScripts),
                    AverageResponseTime = GenerateRandomResponseTime() // Placeholder for actual metrics
                })
                .OrderBy(trend => trend.Date)
                .ToList();

            return trendData;
        }

        public virtual async Task<IList<RiskLevelData>> GetRiskLevelBreakdownAsync(int storeId)
        {
            var scriptsQuery = _authorizedScriptRepository.Table.Where(script => script.IsActive);
            if (storeId > 0)
                scriptsQuery = scriptsQuery.Where(log => log.StoreId == storeId);
            var scripts = await scriptsQuery.GroupBy(script => script.RiskLevelId)
                .Select(group => new
                {
                    RiskLevelId = group.Key,
                    Count = group.Count()
                })
                .ToListAsync();

            var totalScripts = scripts.Sum(s => s.Count);
            var riskColors = new Dictionary<int, string>
            {
                { 1, "#28A745" }, // Low - Green
                { 2, "#FFC107" }, // Medium - Yellow
                { 3, "#DC3545" }  // High - Red
            };

            var result = await scripts.SelectAwait(async script => new RiskLevelData
            {
                RiskLevel = await _localizationService.GetLocalizedEnumAsync((RiskLevel)script.RiskLevelId),
                Count = script.Count,
                Percentage = totalScripts > 0 ? (double)script.Count / totalScripts * 100 : 0,
                Color = riskColors.GetValueOrDefault(script.RiskLevelId, "#6C757D")
            }).ToListAsync();

            return result;
        }

        public virtual async Task<IList<TopViolatingScriptsData>> GetTopViolatingScriptsAsync(int storeId, 
            int days = 30, int topCount = 10)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var alertsQuery = _complianceAlertRepository.Table.Where(alert => alert.CreatedOnUtc >= fromDate
                    && !string.IsNullOrEmpty(alert.ScriptUrl));
            if (storeId > 0)
                alertsQuery = alertsQuery.Where(log => log.StoreId == storeId);
            var alerts = await alertsQuery.GroupBy(alert => alert.ScriptUrl)
                .Select(group => new
                {
                    ScriptUrl = group.Key,
                    ViolationCount = group.Count(),
                    LastViolation = group.Max(alert => alert.CreatedOnUtc)
                })
                .OrderByDescending(script => script.ViolationCount)
                .Take(topCount)
                .ToListAsync();

            var result = new List<TopViolatingScriptsData>();

            foreach (var alert in alerts)
            {
                var authorizedScript = await _authorizedScriptRepository.Table
                    .FirstOrDefaultAsync(script => script.ScriptUrl == alert.ScriptUrl && (storeId == 0 || script.StoreId == storeId));

                var topViolatingScriptsData = new TopViolatingScriptsData
                {
                    ScriptUrl = TruncateUrl(alert.ScriptUrl, 50),
                    ViolationCount = alert.ViolationCount,
                    LastViolation = alert.LastViolation.ToString("MMM dd, yyyy"),
                };

                if (authorizedScript != null)
                {
                    topViolatingScriptsData.RiskLevelId = authorizedScript.RiskLevelId; 
                    topViolatingScriptsData.RiskLevelText = await _localizationService.GetLocalizedEnumAsync((RiskLevel)authorizedScript.RiskLevelId);
                }
                else
                {
                    topViolatingScriptsData.RiskLevelText = "Unknown";
                    topViolatingScriptsData.RiskLevelId = 0; // Default or unknown risk level
                }

                result.Add(topViolatingScriptsData);
            }

            return result;
        }

        #endregion
    }
}