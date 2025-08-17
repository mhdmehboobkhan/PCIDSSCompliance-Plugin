using System.Text.Json;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.IdentityModel.Tokens;
using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Enums;
using Nop.Plugin.Misc.PaymentGuard.Helpers;
using Nop.Plugin.Misc.PaymentGuard.Models;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.PaymentGuard.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    [AutoValidateAntiforgeryToken]
    public class PaymentGuardController : BasePluginController
    {
        private readonly IAuthorizedScriptService _authorizedScriptService;
        private readonly IMonitoringService _monitoringService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly INotificationService _notificationService;
        private readonly ILocalizationService _localizationService;
        private readonly IPermissionService _permissionService;
        private readonly IExportService _exportService;
        private readonly IComplianceAlertService _complianceAlertService;
        private readonly ILogger _logger;
        private readonly IDashboardService _dashboardService;
        private readonly SRIHelper _sriHelper;
        private readonly IBaseAdminModelFactory _baseAdminModelFactory;

        public PaymentGuardController(IAuthorizedScriptService authorizedScriptService,
            IMonitoringService monitoringService,
            ISettingService settingService,
            IStoreService storeService,
            IStoreContext storeContext,
            IWorkContext workContext,
            INotificationService notificationService,
            ILocalizationService localizationService,
            IPermissionService permissionService,
            IExportService exportService,
            IComplianceAlertService complianceAlertService,
            ILogger logger,
            IDashboardService dashboardService,
            SRIHelper sriHelper,
            IBaseAdminModelFactory baseAdminModelFactory)
        {
            _authorizedScriptService = authorizedScriptService;
            _monitoringService = monitoringService;
            _settingService = settingService;
            _storeService = storeService;
            _storeContext = storeContext;
            _workContext = workContext;
            _notificationService = notificationService;
            _localizationService = localizationService;
            _permissionService = permissionService;
            _exportService = exportService;
            _complianceAlertService = complianceAlertService;
            _logger = logger;
            _dashboardService = dashboardService;
            _sriHelper = sriHelper;
            _baseAdminModelFactory = baseAdminModelFactory;
        }

        #region Utilities

        protected virtual async Task PrepareDefaultItemAsync(IList<SelectListItem> items,
            bool withSpecialDefaultItem, string defaultItemText = null, string defaultItemValue = "0")
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            //whether to insert the first special item for the default value
            if (!withSpecialDefaultItem)
                return;

            //prepare item text
            defaultItemText ??= await _localizationService.GetResourceAsync("Admin.Common.All");

            //insert this default item at first
            items.Insert(0, new SelectListItem { Text = defaultItemText, Value = defaultItemValue });
        }

        protected virtual async Task PrepareRiskLevelsAsync(IList<SelectListItem> items,
            bool withSpecialDefaultItem = true, string defaultItemText = null, string defaultItemValue = "0")
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var itemsObj = await RiskLevel.Low.ToSelectListAsync(false);
            foreach (var item in itemsObj)
            {
                items.Add(item);
            }

            //insert special item for the default value
            await PrepareDefaultItemAsync(items, withSpecialDefaultItem, defaultItemText, defaultItemValue);
        }

        protected virtual async Task PrepareScriptSourcesAsync(IList<SelectListItem> items,
            bool withSpecialDefaultItem = true, string defaultItemText = null, string defaultItemValue = "0")
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var itemsObj = await ScriptSource.Internal.ToSelectListAsync(false);
            foreach (var item in itemsObj)
            {
                items.Add(item);
            }

            //insert special item for the default value
            await PrepareDefaultItemAsync(items, withSpecialDefaultItem, defaultItemText, defaultItemValue);
        }

        private async Task<AuthorizedScriptModel> PrepareAuthorizedScriptModelAsync(AuthorizedScriptModel model, AuthorizedScript script)
        {
            var currentStore = await _storeContext.GetCurrentStoreAsync();
            if (script != null && model == null)
            {
                model = new AuthorizedScriptModel
                {
                    Id = script.Id,
                    ScriptUrl = script.ScriptUrl,
                    ScriptHash = script.ScriptHash,
                    Purpose = script.Purpose,
                    Justification = script.Justification,
                    RiskLevelId = script.RiskLevelId,
                    IsActive = script.IsActive,
                    AuthorizedBy = script.AuthorizedBy,
                    AuthorizedOnUtc = script.AuthorizedOnUtc,
                    LastVerifiedUtc = script.LastVerifiedUtc,
                    Source = script.Source
                };
            }

            await PrepareRiskLevelsAsync(model.AvailableRiskLevels, false);
            await PrepareScriptSourcesAsync(model.AvailableSources, false);
            await _baseAdminModelFactory.PrepareStoresAsync(model.AvailableStores, false);

            if (script == null && model.AvailableStores.Count() == 1)
            {
                model.StoreId = currentStore.Id;
            }

            return model;
        }

        #endregion

        #region Configure

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManagePaymentGuard))
                return AccessDeniedView();

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(storeScope);

            var model = new ConfigurationModel
            {
                IsEnabled = settings.IsEnabled,
                EnableEmailAlerts = settings.EnableEmailAlerts,
                EnableCSPHeaders = settings.EnableCSPHeaders,
                EnableSRIValidation = settings.EnableSRIValidation,
                CSPPolicy = settings.CSPPolicy,
                EnableDetailedLogging = settings.EnableDetailedLogging,
                MonitoredPages = settings.MonitoredPages,
                MaxAlertFrequency = settings.MaxAlertFrequency,

                // Add the missing settings
                LogRetentionDays = settings.LogRetentionDays,
                AlertRetentionDays = settings.AlertRetentionDays,
                EnableAutomaticCleanup = settings.EnableAutomaticCleanup,
                CacheExpirationMinutes = settings.CacheExpirationMinutes,
                EnableApiRateLimit = settings.EnableApiRateLimit,
                ApiRateLimitPerHour = settings.ApiRateLimitPerHour,
                WhitelistedIPs = settings.WhitelistedIPs,
                TrustedDomains = settings.TrustedDomains,
                PaymentProviders = settings.PaymentProviders,

                SendWeeklyReports = settings.SendWeeklyReports,
                WeeklyReportDay = settings.WeeklyReportDay,
                LastWeeklyReportSent = settings.LastWeeklyReportSentUtc?.ToString("MMM dd, yyyy HH:mm") + " UTC" ?? "Never",

                ActiveStoreScopeConfiguration = storeScope
            };

            //prepare available days
            var availableDays = await DayOfWeek.Sunday.ToSelectListAsync(false);
            foreach (var day in availableDays)
            {
                model.WeeklyReportDayOptions.Add(day);
            }

            if (storeScope > 0)
            {
                model.IsEnabled_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.IsEnabled, storeScope);
                model.EnableEmailAlerts_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableEmailAlerts, storeScope);
                model.EnableCSPHeaders_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableCSPHeaders, storeScope);
                model.EnableSRIValidation_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableSRIValidation, storeScope);
                model.CSPPolicy_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.CSPPolicy, storeScope);
                model.EnableDetailedLogging_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableDetailedLogging, storeScope);
                model.MonitoredPages_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.MonitoredPages, storeScope);
                model.MaxAlertFrequency_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.MaxAlertFrequency, storeScope);

                // Add the missing override checks
                model.LogRetentionDays_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.LogRetentionDays, storeScope);
                model.AlertRetentionDays_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.AlertRetentionDays, storeScope);
                model.EnableAutomaticCleanup_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableAutomaticCleanup, storeScope);
                model.CacheExpirationMinutes_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.CacheExpirationMinutes, storeScope);
                model.EnableApiRateLimit_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableApiRateLimit, storeScope);
                model.ApiRateLimitPerHour_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ApiRateLimitPerHour, storeScope);
                model.WhitelistedIPs_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.WhitelistedIPs, storeScope);
                model.TrustedDomains_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.TrustedDomains, storeScope);
                model.PaymentProviders_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.PaymentProviders, storeScope);

                model.SendWeeklyReports_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.SendWeeklyReports, storeScope);
                model.WeeklyReportDay_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.WeeklyReportDay, storeScope);
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManagePaymentGuard))
                return AccessDeniedView();

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(storeScope);

            settings.IsEnabled = model.IsEnabled;
            settings.EnableEmailAlerts = model.EnableEmailAlerts;
            settings.EnableCSPHeaders = model.EnableCSPHeaders;
            settings.EnableSRIValidation = model.EnableSRIValidation;
            settings.CSPPolicy = model.CSPPolicy;
            settings.EnableDetailedLogging = model.EnableDetailedLogging;
            settings.MonitoredPages = model.MonitoredPages;
            settings.MaxAlertFrequency = model.MaxAlertFrequency;

            // Add the missing settings assignments
            settings.LogRetentionDays = model.LogRetentionDays;
            settings.AlertRetentionDays = model.AlertRetentionDays;
            settings.EnableAutomaticCleanup = model.EnableAutomaticCleanup;
            settings.CacheExpirationMinutes = model.CacheExpirationMinutes;
            settings.EnableApiRateLimit = model.EnableApiRateLimit;
            settings.ApiRateLimitPerHour = model.ApiRateLimitPerHour;
            settings.WhitelistedIPs = model.WhitelistedIPs;
            settings.TrustedDomains = model.TrustedDomains;
            settings.PaymentProviders = model.PaymentProviders;

            settings.SendWeeklyReports = model.SendWeeklyReports;
            settings.WeeklyReportDay = model.WeeklyReportDay;

            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.IsEnabled, model.IsEnabled_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableEmailAlerts, model.EnableEmailAlerts_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableCSPHeaders, model.EnableCSPHeaders_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableSRIValidation, model.EnableSRIValidation_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.CSPPolicy, model.CSPPolicy_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableDetailedLogging, model.EnableDetailedLogging_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.MonitoredPages, model.MonitoredPages_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.MaxAlertFrequency, model.MaxAlertFrequency_OverrideForStore, storeScope, false);

            // Add the missing SaveSettingOverridablePerStoreAsync calls
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.LogRetentionDays, model.LogRetentionDays_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.AlertRetentionDays, model.AlertRetentionDays_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableAutomaticCleanup, model.EnableAutomaticCleanup_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.CacheExpirationMinutes, model.CacheExpirationMinutes_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableApiRateLimit, model.EnableApiRateLimit_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.ApiRateLimitPerHour, model.ApiRateLimitPerHour_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.WhitelistedIPs, model.WhitelistedIPs_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.TrustedDomains, model.TrustedDomains_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.PaymentProviders, model.PaymentProviders_OverrideForStore, storeScope, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.SendWeeklyReports, model.SendWeeklyReports_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.WeeklyReportDay, model.WeeklyReportDay_OverrideForStore, storeScope, false);

            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return RedirectToAction("Configure");
        }

        #endregion

        #region Authorized Scripts

        public async Task<IActionResult> List()
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            var currentStore = await _storeContext.GetCurrentStoreAsync();
            var model = new AuthorizedScriptSearchModel()
            {
                SearchStoreId = currentStore.Id
            };
            await PrepareRiskLevelsAsync(model.AvailableRiskLevels, true, defaultItemValue: "");
            await PrepareScriptSourcesAsync(model.AvailableSources, true, defaultItemValue: "");
            await _baseAdminModelFactory.PrepareStoresAsync(model.AvailableStores, false);

            model.AvailableActiveOptions = new List<SelectListItem>
            {
                new() { Value = "0", Text = "All" },
                new() { Value = "1", Text = "Active" },
                new() { Value = "2", Text = "In-Active" },
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> List(AuthorizedScriptSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return await AccessDeniedDataTablesJson();

            var overrideIsActive = searchModel.SearchIsActiveId == 0 ? null : (bool?)(searchModel.SearchIsActiveId == 1);

            var scripts = await _authorizedScriptService.GetAllAuthorizedScriptsAsync(storeId: searchModel.SearchStoreId,
                scriptUrl: searchModel.SearchScriptUrl,
                riskLevelId: searchModel.SearchRiskLevelId,
                sourceType: searchModel.SearchSource,
                isActive: overrideIsActive,
                pageIndex: searchModel.Page - 1,
                pageSize: searchModel.PageSize);

            var model = await new AuthorizedScriptListModel().PrepareToGridAsync(searchModel, scripts, () =>
            {
                return scripts.SelectAwait(async script => new AuthorizedScriptModel
                {
                    Id = script.Id,
                    ScriptUrl = script.ScriptUrl,
                    Purpose = script.Purpose,
                    RiskLevelId = script.RiskLevelId,
                    RiskLevelText = await _localizationService.GetLocalizedEnumAsync(script.RiskLevel),
                    IsActive = script.IsActive,
                    AuthorizedBy = script.AuthorizedBy,
                    AuthorizedOnUtc = script.AuthorizedOnUtc,
                    LastVerifiedUtc = script.LastVerifiedUtc,
                    Source = script.Source
                });
            });

            return Json(model);
        }

        public async Task<IActionResult> Create(string scriptUrl = "")
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            var currentStore = await _storeContext.GetCurrentStoreAsync();
            var model = new AuthorizedScriptModel()
            {
                ScriptUrl = scriptUrl,
                StoreId = currentStore.Id
            };
            await PrepareAuthorizedScriptModelAsync(model, null);

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        public async Task<IActionResult> Create(AuthorizedScriptModel model, bool continueEditing)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            if (ModelState.IsValid)
            {
                var currentUser = await _workContext.GetCurrentCustomerAsync();

                // Check if script already exists
                var existingScript = await _authorizedScriptService.GetAuthorizedScriptByUrlAsync(model.ScriptUrl, model.StoreId);
                if (existingScript != null)
                {
                    ModelState.AddModelError("ScriptUrl", "A script with this URL already exists.");
                    await PrepareAuthorizedScriptModelAsync(model, null);
                    return View(model);
                }

                var script = new AuthorizedScript
                {
                    ScriptUrl = model.ScriptUrl,
                    Purpose = model.Purpose,
                    Justification = model.Justification,
                    RiskLevelId = model.RiskLevelId,
                    IsActive = model.IsActive,
                    AuthorizedBy = currentUser.Email,
                    AuthorizedOnUtc = DateTime.UtcNow,
                    LastVerifiedUtc = DateTime.UtcNow,
                    Source = model.Source,
                    StoreId = model.StoreId
                };

                // Extract domain from URL
                try
                {
                    var uri = new Uri(script.ScriptUrl);
                    script.Domain = uri.Host;
                }
                catch
                {
                    script.Domain = "unknown";
                }

                // Generate hash if script is external
                if (model.GenerateHash && script.ScriptUrl.StartsWith("http"))
                {
                    script.ScriptHash = await _sriHelper.GenerateExternalSRIHashAsync(script.ScriptUrl);
                    script.HashAlgorithm = "sha384";
                }

                await _authorizedScriptService.InsertAuthorizedScriptAsync(script);

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptAdded"));

                if (!continueEditing)
                    return RedirectToAction("List");

                return RedirectToAction("Edit", new { id = script.Id });
            }

            await PrepareAuthorizedScriptModelAsync(model, null);
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            var script = await _authorizedScriptService.GetAuthorizedScriptByIdAsync(id);
            if (script == null)
                return RedirectToAction("List");

            var model = await PrepareAuthorizedScriptModelAsync(null, script);

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        public async Task<IActionResult> Edit(AuthorizedScriptModel model, bool continueEditing)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            var script = await _authorizedScriptService.GetAuthorizedScriptByIdAsync(model.Id);
            if (script == null)
                return RedirectToAction("List");

            if (ModelState.IsValid)
            {
                script.ScriptUrl = model.ScriptUrl;
                script.Purpose = model.Purpose;
                script.Justification = model.Justification;
                script.RiskLevelId = model.RiskLevelId;
                script.IsActive = model.IsActive;
                script.Source = model.Source;
                script.StoreId = model.StoreId;

                // Update domain if URL changed
                try
                {
                    var uri = new Uri(script.ScriptUrl);
                    script.Domain = uri.Host;
                }
                catch
                {
                    script.Domain = "unknown";
                }

                // Regenerate hash if requested
                if (model.GenerateHash && script.ScriptUrl.StartsWith("http"))
                {
                    script.ScriptHash = await _sriHelper.GenerateExternalSRIHashAsync(script.ScriptUrl);
                    script.LastVerifiedUtc = DateTime.UtcNow;
                }

                await _authorizedScriptService.UpdateAuthorizedScriptAsync(script);

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptUpdated"));

                if (!continueEditing)
                    return RedirectToAction("List");

                return RedirectToAction("Edit", new { id = script.Id });
            }

            await PrepareAuthorizedScriptModelAsync(model, script);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            var script = await _authorizedScriptService.GetAuthorizedScriptByIdAsync(id);
            if (script != null)
            {
                await _authorizedScriptService.DeleteAuthorizedScriptAsync(script);
                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptDeleted"));
            }

            return RedirectToAction("List");
        }

        #endregion

        #region Monitoring Logs

        public async Task<IActionResult> Dashboard(int days = 30)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManagePaymentGuard))
                return AccessDeniedView();

            var currentStore = await _storeContext.GetCurrentStoreAsync();
            var model = new DashboardModel();
            try
            {
                model = await _dashboardService.GetDashboardDataAsync(currentStore.Id, days);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error loading PaymentGuard dashboard", ex);
                _notificationService.ErrorNotification("Error loading dashboard data");

                // Return basic model in case of error
                model = new DashboardModel
                {
                    TotalScriptsMonitored = 0,
                    AuthorizedScriptsCount = 0,
                    UnauthorizedScriptsCount = 0,
                    ComplianceScore = 0,
                    LastCheckDate = DateTime.UtcNow,
                    TotalChecksPerformed = 0,
                    AlertsGenerated = 0
                };
            }
            model.SearchStoreId = currentStore.Id;
            await _baseAdminModelFactory.PrepareStoresAsync(model.AvailableStores, false);

            return View(model);
        }

        // Add method for dashboard data refresh via AJAX
        [HttpPost]
        public async Task<IActionResult> RefreshDashboardData(int storeId, int days = 30)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManagePaymentGuard))
                return Json(new { success = false, message = "Access denied" });

            try
            {
                var model = await _dashboardService.GetDashboardDataAsync(storeId, days);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        complianceScore = model.ComplianceScore,
                        totalScripts = model.TotalScriptsMonitored,
                        authorizedScripts = model.AuthorizedScriptsCount,
                        unauthorizedScripts = model.UnauthorizedScriptsCount,
                        alertsGenerated = model.AlertsGenerated,
                        lastCheck = model.LastCheckDate.ToString("MMM dd, yyyy HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error refreshing dashboard data", ex);
                return Json(new { success = false, message = "Error refreshing dashboard" });
            }
        }

        public async Task<IActionResult> MonitoringLogs()
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return AccessDeniedView();

            var currentStore = await _storeContext.GetCurrentStoreAsync();
            var model = new MonitoringLogSearchModel()
            {
                SearchStoreId = currentStore.Id
            };

            // Prepare dropdown lists
            model.AvailableUnauthorizedOptions = new List<SelectListItem>
            {
                new() { Value = "", Text = await _localizationService.GetResourceAsync("Admin.Common.All") },
                new() { Value = "false", Text = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.MonitoringLogs.Compliant") },
                new() { Value = "true", Text = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.MonitoringLogs.HasIssues") },
            };
            await _baseAdminModelFactory.PrepareStoresAsync(model.AvailableStores, false);

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> MonitoringLogsList(MonitoringLogSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return await AccessDeniedDataTablesJson();

            bool? hasUnauthorizedScripts = null;
            if (!string.IsNullOrEmpty(searchModel.SearchHasUnauthorizedScripts) &&
                bool.TryParse(searchModel.SearchHasUnauthorizedScripts, out var hasIssues))
                hasUnauthorizedScripts = hasIssues;

            var logs = await _monitoringService.GetMonitoringLogsAsync(storeId: searchModel.SearchStoreId,
                fromDate: searchModel.SearchDateFrom,
                toDate: searchModel.SearchDateTo,
                hasUnauthorizedScripts: hasUnauthorizedScripts,
                pageIndex: searchModel.Page - 1,
                pageSize: searchModel.PageSize);

            var model = new MonitoringLogListModel().PrepareToGrid(searchModel, logs, () =>
            {
                return logs.Select(log => new MonitoringLogModel
                {
                    Id = log.Id,
                    PageUrl = log.PageUrl,
                    TotalScriptsFound = log.TotalScriptsFound,
                    AuthorizedScriptsCount = log.AuthorizedScriptsCount,
                    UnauthorizedScriptsCount = log.UnauthorizedScriptsCount,
                    HasUnauthorizedScripts = log.HasUnauthorizedScripts,
                    CheckedOnUtc = log.CheckedOnUtc,
                    CheckType = log.CheckType,
                    AlertSent = log.AlertSent
                });
            });

            return Json(model);
        }

        public async Task<IActionResult> MonitoringLogDetails(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return AccessDeniedView();

            var log = await _monitoringService.GetMonitoringLogByIdAsync(id);
            if (log == null)
                return RedirectToAction("MonitoringLogs");

            var model = new MonitoringLogModel
            {
                Id = log.Id,
                PageUrl = log.PageUrl,
                TotalScriptsFound = log.TotalScriptsFound,
                AuthorizedScriptsCount = log.AuthorizedScriptsCount,
                UnauthorizedScriptsCount = log.UnauthorizedScriptsCount,
                HasUnauthorizedScripts = log.HasUnauthorizedScripts,
                CheckedOnUtc = log.CheckedOnUtc,
                CheckType = log.CheckType,
                AlertSent = log.AlertSent
            };

            try
            {
                if (!string.IsNullOrEmpty(log.DetectedScripts))
                    model.DetectedScripts = JsonSerializer.Deserialize<List<string>>(log.DetectedScripts) ?? new List<string>();

                if (!string.IsNullOrEmpty(log.UnauthorizedScripts))
                    model.UnauthorizedScripts = JsonSerializer.Deserialize<List<string>>(log.UnauthorizedScripts) ?? new List<string>();

                if (!string.IsNullOrEmpty(log.HttpHeaders))
                    model.SecurityHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(log.HttpHeaders) ?? new Dictionary<string, string>();
            }
            catch (JsonException ex)
            {
                await _logger.ErrorAsync($"Error deserializing monitoring log data for log ID {id}", ex);
            }

            return View(model);
        }

        #endregion

        #region Export Actions

        [HttpPost]
        public async Task<IActionResult> ExportScriptsToCsv(AuthorizedScriptSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            try
            {
                var overrideIsActive = searchModel.SearchIsActiveId == 0 ? null : (bool?)(searchModel.SearchIsActiveId == 1);

                // Get all scripts based on search criteria
                var scripts = await _authorizedScriptService.GetAllAuthorizedScriptsAsync(storeId: searchModel.SearchStoreId,
                    scriptUrl: searchModel.SearchScriptUrl,
                    riskLevelId: searchModel.SearchRiskLevelId,
                    sourceType: searchModel.SearchSource,
                    isActive: overrideIsActive);

                var csvData = await _exportService.ExportAuthorizedScriptsToCsvAsync(scripts.ToList());

                var fileName = $"authorized-scripts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                return File(csvData, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error exporting authorized scripts to CSV", ex);
                _notificationService.ErrorNotification("Error exporting data to CSV");
                return RedirectToAction("List");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportMonitoringLogsToCsv(MonitoringLogSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return AccessDeniedView();

            try
            {
                bool? hasUnauthorizedScripts = null;
                if (!string.IsNullOrEmpty(searchModel.SearchHasUnauthorizedScripts) &&
                    bool.TryParse(searchModel.SearchHasUnauthorizedScripts, out var hasIssues))
                    hasUnauthorizedScripts = hasIssues;

                var logs = await _monitoringService.GetMonitoringLogsAsync(storeId: searchModel.SearchStoreId,
                    fromDate: searchModel.SearchDateFrom,
                    toDate: searchModel.SearchDateTo,
                    hasUnauthorizedScripts: hasUnauthorizedScripts);

                var csvData = await _exportService.ExportMonitoringLogsToCsvAsync(logs.ToList());
                var fileName = $"monitoring-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                return File(csvData, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error exporting monitoring logs to CSV", ex);
                _notificationService.ErrorNotification("Error exporting data to CSV");
                return RedirectToAction("MonitoringLogs");
            }
        }

        public async Task<IActionResult> GenerateComplianceReport()
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return AccessDeniedView();

            return await ComplianceReport(new ComplianceReportModel());
        }

        [HttpPost]
        public async Task<IActionResult> GenerateComplianceReport(ComplianceReportModel model)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return AccessDeniedView();

            return await ComplianceReport(model);
        }

        [HttpPost, ActionName("GenerateComplianceReport")]
        [FormValueRequired("btnDownloadComplianceReport")]
        public async Task<IActionResult> DownloadComplianceReport(ComplianceReportModel model)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return AccessDeniedView();

            return await ComplianceReport(model, "pdf");
        }

        public async Task<IActionResult> ComplianceReport(ComplianceReportModel model, string format = "")
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return AccessDeniedView();

            try
            {
                if (format.ToLower() == "pdf")
                {
                    var pdfData = await _exportService.GenerateComplianceReportPdfAsync(model.SearchStoreId, model.FromDate, model.ToDate);
                    var fileName = $"compliance-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
                    return File(pdfData, "application/pdf", fileName);
                }
                else
                {
                    // Default to HTML preview
                    var report = await _monitoringService.GenerateComplianceReportAsync(model.SearchStoreId, model.FromDate, model.ToDate);
                    model.Report = report;

                    if (model.StoreId > 0)
                    {
                        var store = await _storeService.GetStoreByIdAsync(model.SearchStoreId);
                        model.StoreName = store.Name;
                    }
                    else
                    {
                        var currentStore = await _storeContext.GetCurrentStoreAsync();
                        model.StoreId = currentStore.Id;
                        model.StoreName = currentStore.Name;
                    }

                    await _baseAdminModelFactory.PrepareStoresAsync(model.AvailableStores, false);
                    return View("ComplianceReport", model);
                }
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error generating compliance report", ex);
                _notificationService.ErrorNotification("Error generating compliance report");
                return RedirectToAction("Dashboard");
            }
        }

        #endregion
    }
}