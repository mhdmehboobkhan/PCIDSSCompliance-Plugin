using Nop.Core;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.PaymentGuard.Tasks
{
    /// <summary>
    /// Task for sending weekly compliance reports
    /// </summary>
    public partial class WeeklyComplianceReportTask : IScheduleTask
    {
        private readonly ILogger _logger;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IDashboardService _dashboardService;
        private readonly IMonitoringService _monitoringService;
        private readonly IEmailAlertService _emailAlertService;
        private readonly ILocalizationService _localizationService;
        private readonly IWorkContext _workContext;

        public WeeklyComplianceReportTask(
            ILogger logger,
            IStoreService storeService,
            ISettingService settingService,
            IDashboardService dashboardService,
            IMonitoringService monitoringService,
            IEmailAlertService emailAlertService,
            ILocalizationService localizationService,
            IWorkContext workContext)
        {
            _logger = logger;
            _storeService = storeService;
            _settingService = settingService;
            _dashboardService = dashboardService;
            _monitoringService = monitoringService;
            _emailAlertService = emailAlertService;
            _localizationService = localizationService;
            _workContext = workContext;
        }

        /// <summary>
        /// Execute task
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task ExecuteAsync()
        {
            try
            {
                await _logger.InformationAsync("PaymentGuard: Starting weekly compliance report task");

                var stores = await _storeService.GetAllStoresAsync();
                var totalReportsSent = 0;
                var totalErrors = 0;

                foreach (var store in stores)
                {
                    try
                    {
                        // Load PaymentGuard settings for this store
                        var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(store.Id);

                        // Check if weekly reports are enabled and email is configured
                        if (!settings.SendWeeklyReports)
                        {
                            await _logger.InformationAsync($"PaymentGuard: Weekly reports disabled for store '{store.Name}' (ID: {store.Id})");
                            continue;
                        }

                        // Check if it's the right day to send (default: Monday)
                        var dayOfWeek = (DayOfWeek)(settings.WeeklyReportDay);
                        if (DateTime.UtcNow.DayOfWeek != dayOfWeek)
                        {
                            await _logger.InformationAsync($"PaymentGuard: Not the scheduled day for weekly report for store '{store.Name}'. Scheduled: {dayOfWeek}, Today: {DateTime.UtcNow.DayOfWeek}");
                            continue;
                        }

                        // Get compliance report data
                        var reportData = await _monitoringService.GenerateComplianceReportAsync(store.Id);

                        // Check if there's meaningful data to report
                        if (reportData.TotalScriptsMonitored == 0 && reportData.TotalChecksPerformed == 0)
                        {
                            await _logger.InformationAsync($"PaymentGuard: No monitoring data available for store '{store.Name}' (ID: {store.Id}), skipping weekly report");
                            continue;
                        }

                        // Send the compliance report
                        var emailIds = await _emailAlertService.SendComplianceReportAsync(reportData, 
                            store.Id);

                        if (emailIds.Any())
                        {
                            totalReportsSent++;
                            await _logger.InformationAsync($"PaymentGuard: Weekly compliance report sent successfully for store '{store.Name}' (ID: {store.Id}). Email IDs: {string.Join(", ", emailIds)}");

                            // Update last sent date in settings
                            settings.LastWeeklyReportSentUtc = DateTime.UtcNow;
                            await _settingService.SaveSettingAsync(settings, store.Id);
                        }
                        else
                        {
                            totalErrors++;
                            await _logger.ErrorAsync($"PaymentGuard: Failed to send weekly compliance report for store '{store.Name}' (ID: {store.Id}) - no emails were queued");
                        }

                        // Add small delay between stores to avoid overwhelming the email system
                        await Task.Delay(1000);
                    }
                    catch (Exception storeException)
                    {
                        totalErrors++;
                        await _logger.ErrorAsync($"PaymentGuard: Error processing weekly compliance report for store '{store.Name}' (ID: {store.Id})", storeException);
                    }
                }

                // Log summary
                await _logger.InformationAsync($"PaymentGuard: Weekly compliance report task completed. Reports sent: {totalReportsSent}, Errors: {totalErrors}");

                // If there were errors, log a warning
                if (totalErrors > 0)
                {
                    await _logger.WarningAsync($"PaymentGuard: Weekly compliance report task completed with {totalErrors} error(s). Check logs for details.");
                }
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("PaymentGuard: Fatal error in weekly compliance report task", ex);
                throw; // Re-throw to mark task as failed
            }
        }
    }
}