using Nop.Data;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.PaymentGuard.Tasks
{
    /// <summary>
    /// PaymentGuard cleanup task - removes old logs and resolved alerts
    /// </summary>
    public partial class PaymentGuardCleanupTask : IScheduleTask
    {
        #region Fields

        private readonly IRepository<ScriptMonitoringLog> _monitoringLogRepository;
        private readonly IRepository<ComplianceAlert> _complianceAlertRepository;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly ILogger _logger;

        #endregion

        #region Ctor

        public PaymentGuardCleanupTask(
            IRepository<ScriptMonitoringLog> monitoringLogRepository,
            IRepository<ComplianceAlert> complianceAlertRepository,
            ISettingService settingService,
            IStoreService storeService,
            ILogger logger)
        {
            _monitoringLogRepository = monitoringLogRepository;
            _complianceAlertRepository = complianceAlertRepository;
            _settingService = settingService;
            _storeService = storeService;
            _logger = logger;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get log retention period in days from settings
        /// </summary>
        /// <param name="settings">PaymentGuard settings</param>
        /// <returns>Retention days</returns>
        private static int GetLogRetentionDays(PaymentGuardSettings settings)
        {
            // Use setting value, default to 90 days if not set properly
            return settings.LogRetentionDays > 0 ? settings.LogRetentionDays : 90;
        }

        /// <summary>
        /// Get alert retention period in days from settings
        /// </summary>
        /// <param name="settings">PaymentGuard settings</param>
        /// <returns>Retention days</returns>
        private static int GetAlertRetentionDays(PaymentGuardSettings settings)
        {
            // Use setting value, default to 30 days if not set properly
            return settings.AlertRetentionDays > 0 ? settings.AlertRetentionDays : 30;
        }

        /// <summary>
        /// Clean up duplicate alerts keeping only the latest occurrence
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        private async Task CleanupDuplicateAlerts(int storeId)
        {
            try
            {
                // Find duplicate alerts by AlertType, ScriptUrl, and PageUrl
                var duplicateGroups = await _complianceAlertRepository.Table
                    .Where(alert => alert.StoreId == storeId && alert.IsResolved)
                    .GroupBy(alert => new { alert.AlertType, alert.ScriptUrl, alert.PageUrl })
                    .Where(group => group.Count() > 1)
                    .ToListAsync();

                var deletedCount = 0;

                foreach (var group in duplicateGroups)
                {
                    // Keep the most recent alert, delete the rest
                    var alertsToDelete = group
                        .OrderByDescending(alert => alert.CreatedOnUtc)
                        .Skip(1)
                        .ToList();

                    if (alertsToDelete.Any())
                    {
                        await _complianceAlertRepository.DeleteAsync(alertsToDelete);
                        deletedCount += alertsToDelete.Count;
                    }
                }

                if (deletedCount > 0)
                {
                    await _logger.InformationAsync($"Cleaned up {deletedCount} duplicate alerts for store {storeId}");
                }
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error cleaning up duplicate alerts for store {storeId}", ex);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Executes the cleanup task
        /// </summary>
        public async Task ExecuteAsync()
        {
            try
            {
                await _logger.InformationAsync("PaymentGuard cleanup task started");

                var stores = await _storeService.GetAllStoresAsync();
                var totalLogsDeleted = 0;
                var totalAlertsDeleted = 0;

                foreach (var store in stores)
                {
                    var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(store.Id);

                    if (!settings.IsEnabled || !settings.EnableAutomaticCleanup)
                    {
                        await _logger.InformationAsync($"Cleanup skipped for store {store.Name} - PaymentGuard disabled or automatic cleanup disabled");
                        continue;
                    }

                    // Clean up old monitoring logs
                    var logRetentionDays = GetLogRetentionDays(settings);
                    var logCutoffDate = DateTime.UtcNow.AddDays(-logRetentionDays);

                    var oldLogs = await _monitoringLogRepository.Table
                        .Where(log => log.StoreId == store.Id && log.CheckedOnUtc < logCutoffDate)
                        .ToListAsync();

                    if (oldLogs.Any())
                    {
                        await _monitoringLogRepository.DeleteAsync(oldLogs);
                        totalLogsDeleted += oldLogs.Count;
                        await _logger.InformationAsync($"Deleted {oldLogs.Count} old monitoring logs (older than {logRetentionDays} days) for store {store.Name}");
                    }

                    // Clean up resolved alerts
                    var alertRetentionDays = GetAlertRetentionDays(settings);
                    var alertCutoffDate = DateTime.UtcNow.AddDays(-alertRetentionDays);

                    var oldResolvedAlerts = await _complianceAlertRepository.Table
                        .Where(alert => alert.StoreId == store.Id
                            && alert.IsResolved
                            && alert.ResolvedOnUtc.HasValue
                            && alert.ResolvedOnUtc.Value < alertCutoffDate)
                        .ToListAsync();

                    if (oldResolvedAlerts.Any())
                    {
                        await _complianceAlertRepository.DeleteAsync(oldResolvedAlerts);
                        totalAlertsDeleted += oldResolvedAlerts.Count;
                        await _logger.InformationAsync($"Deleted {oldResolvedAlerts.Count} old resolved alerts (older than {alertRetentionDays} days) for store {store.Name}");
                    }

                    // Clean up duplicate alerts (keep only the latest occurrence)
                    await CleanupDuplicateAlerts(store.Id);
                }

                await _logger.InformationAsync($"PaymentGuard cleanup task completed. Deleted {totalLogsDeleted} logs and {totalAlertsDeleted} alerts");
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error executing PaymentGuard cleanup task", ex);
                throw;
            }
        }

        #endregion
    }
}