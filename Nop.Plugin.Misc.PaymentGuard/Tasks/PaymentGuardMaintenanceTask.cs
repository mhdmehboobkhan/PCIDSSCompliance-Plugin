using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Helpers;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.PaymentGuard.Tasks
{
    public partial class PaymentGuardMaintenanceTask : IScheduleTask
    {
        #region Fields

        private readonly IAuthorizedScriptService _authorizedScriptService;
        private readonly IEmailAlertService _emailAlertService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IStoreService _storeService;
        private readonly ILogger _logger;
        private readonly SRIHelper _sriHelper;

        #endregion

        #region Ctor

        public PaymentGuardMaintenanceTask(IAuthorizedScriptService authorizedScriptService,
            IEmailAlertService emailAlertService,
            ISettingService settingService,
            IStoreContext storeContext,
            IStoreService storeService,
            ILogger logger,
            SRIHelper sriHelper)
        {
            _authorizedScriptService = authorizedScriptService;
            _emailAlertService = emailAlertService;
            _settingService = settingService;
            _storeContext = storeContext;
            _storeService = storeService;
            _logger = logger;
            _sriHelper = sriHelper;
        }

        #endregion

        #region Utilities

        public async Task UpdateScriptsSRIHashAsync(int storeId, string storeName)
        {
            var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>();
            if (!settings.IsEnabled)
                return;

            // Check for expired scripts (haven't been verified in 30 days)
            var expiredScripts = await _authorizedScriptService.GetExpiredScriptsAsync(30, storeId);
            if (expiredScripts != null && expiredScripts.Any())
            {
                await _logger.WarningAsync($"Found {expiredScripts.Count} expired scripts for store {storeName}");

                // Send alert email about expired scripts
                await _emailAlertService.SendExpiredScriptsAlertAsync(expiredScripts, storeId);

                // Auto-update hashes for scripts that are still accessible
                foreach (var script in expiredScripts)
                {
                    try
                    {
                        var isValid = await _authorizedScriptService.ValidateScriptIntegrityAsync(script.ScriptUrl, script.ScriptHash);
                        if (!isValid)
                        {
                            // Try to update the hash
                            var newHash = await _sriHelper.GenerateExternalSRIHashAsync(script.ScriptUrl);
                            if (!string.IsNullOrEmpty(newHash))
                            {
                                await _authorizedScriptService.UpdateScriptHashAsync(script.Id, newHash);
                                await _logger.InformationAsync($"Updated hash for script: {script.ScriptUrl}");
                            }
                        }
                        else
                        {
                            // Hash is still valid, just update the verification date
                            script.LastVerifiedUtc = DateTime.UtcNow;
                            await _authorizedScriptService.UpdateAuthorizedScriptAsync(script);
                        }
                    }
                    catch (Exception ex)
                    {
                        await _logger.ErrorAsync($"Error processing expired script {script.ScriptUrl}", ex);
                    }
                }
            }
        }

        #endregion

        #region Methods

        public async Task ExecuteAsync()
        {
            try
            {
                await _logger.InformationAsync("PaymentGuard maintenance task started");

                var stores = await _storeService.GetAllStoresAsync();
                foreach (var store in stores)
                {
                    await UpdateScriptsSRIHashAsync(store.Id, store.Name);

                    await Task.Delay(1000);
                }

                await _logger.InformationAsync("PaymentGuard maintenance task completed");
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error executing PaymentGuard maintenance task", ex);
                throw;
            }
        }

        #endregion
    }
}