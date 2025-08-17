using Nop.Plugin.Misc.PaymentGuard.Models;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial interface IDashboardService
    {
        /// <summary>
        /// Get comprehensive dashboard data with charts and metrics
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="days">Number of days to analyze</param>
        /// <returns>Dashboard model with analytics data</returns>
        Task<DashboardModel> GetDashboardDataAsync(int storeId, int days = 30);

        /// <summary>
        /// Get compliance history for chart
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="days">Number of days</param>
        /// <returns>Compliance history data points</returns>
        Task<IList<ComplianceChartDataPoint>> GetComplianceHistoryAsync(int storeId, int days = 30);

        /// <summary>
        /// Get alert type distribution for pie chart
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="days">Number of days</param>
        /// <returns>Alert type distribution data</returns>
        Task<IList<AlertTypeChartData>> GetAlertTypeDistributionAsync(int storeId, int days = 30);

        /// <summary>
        /// Get monitoring trends for line chart
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="days">Number of days</param>
        /// <returns>Monitoring trend data</returns>
        Task<IList<MonitoringTrendData>> GetMonitoringTrendsAsync(int storeId, int days = 30);

        /// <summary>
        /// Get risk level breakdown for chart
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <returns>Risk level breakdown data</returns>
        Task<IList<RiskLevelData>> GetRiskLevelBreakdownAsync(int storeId);

        /// <summary>
        /// Get top violating scripts
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="days">Number of days</param>
        /// <param name="topCount">Number of top scripts to return</param>
        /// <returns>Top violating scripts data</returns>
        Task<IList<TopViolatingScriptsData>> GetTopViolatingScriptsAsync(int storeId, int days = 30, int topCount = 10);
    }
}