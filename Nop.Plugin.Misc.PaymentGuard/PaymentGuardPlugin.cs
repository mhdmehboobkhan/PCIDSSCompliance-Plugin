using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Core.Domain.Security;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.PaymentGuard.Components;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.Misc.PaymentGuard
{
    /// <summary>
    /// The License Spring plugin
    /// </summary>
    public partial class PaymentGuardPlugin : BasePlugin, IWidgetPlugin, IMiscPlugin, IAdminMenuPlugin
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly IWebHelper _webHelper;
        private readonly ISettingService _settingService;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly PaymentGuardSettings _paymentGuardSettings;
        private readonly IPermissionService _permissionService;
        private readonly WidgetSettings _widgetSettings;
        private readonly IStoreService _storeService;
        private readonly IMessageTemplateService _messageTemplateService;
        
        #endregion

        #region Ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentGuardPlugin" /> class.
        /// </summary>
        /// <param name="emailAccountService">The email account service.</param>
        /// <param name="genericAttributeService">The generic attribute service.</param>
        /// <param name="localizationService">The localization service.</param>
        /// <param name="messageTemplateService">The message template service.</param>
        /// <param name="scheduleTaskService">The schedule task service.</param>
        /// <param name="settingService">The setting service.</param>
        /// <param name="storeService">The store service.</param>
        /// <param name="webHelper">The web helper.</param>
        /// <param name="widgetSettings">The widget settings.</param>
        public PaymentGuardPlugin(ILocalizationService localizationService,
            IWebHelper webHelper,
            ISettingService settingService,
            IScheduleTaskService scheduleTaskService,
            PaymentGuardSettings paymentGuardSettings,
            IPermissionService permissionService,
            WidgetSettings widgetSettings,
            IStoreService storeService,
            IMessageTemplateService messageTemplateService)
        {
            _localizationService = localizationService;
            _webHelper = webHelper;
            _settingService = settingService;
            _scheduleTaskService = scheduleTaskService;
            _paymentGuardSettings = paymentGuardSettings;
            _permissionService = permissionService;
            _widgetSettings = widgetSettings;
            _storeService = storeService;
            _messageTemplateService = messageTemplateService;
        }

        #endregion

        #region Utilties

        private async Task InstallPermissionsAsync()
        {
            var permissionProvider = new PaymentGuardPermissionProvider();
            var allPermissions = await _permissionService.GetAllPermissionRecordsAsync();

            foreach (var permission in permissionProvider.GetPermissions())
            {
                // Check if permission already exists
                var existingPermission = allPermissions.Where(x => x.SystemName == permission.SystemName).FirstOrDefault();
                if (existingPermission == null)
                {
                    await _permissionService.InsertPermissionRecordAsync(permission);
                }
                else
                {
                    // Update the existing permission's Id for role mapping
                    permission.Id = existingPermission.Id;
                }
            }

            // Install default permissions for administrators
            var customerService = EngineContext.Current.Resolve<ICustomerService>();
            var adminRole = await customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.AdministratorsRoleName);

            if (adminRole != null)
            {
                foreach (var permission in permissionProvider.GetPermissions())
                {
                    // Check if the permission is already assigned to the admin role
                    var existingMapping = await _permissionService.GetMappingByPermissionRecordIdAsync(permission.Id);
                    var isAlreadyAssigned = existingMapping.Any(m => m.CustomerRoleId == adminRole.Id);

                    if (!isAlreadyAssigned)
                    {
                        await _permissionService.InsertPermissionRecordCustomerRoleMappingAsync(new PermissionRecordCustomerRoleMapping
                        {
                            CustomerRoleId = adminRole.Id,
                            PermissionRecordId = permission.Id
                        });
                    }
                }
            }
        }

        private async Task InstallScheduleTaskAsync()
        {
            // Install monotoring task
            //if (await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.MonitoringTask") == null)
            //{
            //    await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
            //    {
            //        Name = "PaymentGuard Monitoring Task",
            //        Seconds = 604800, // Weekly (7 days * 24 hours * 60 minutes * 60 seconds)
            //        Type = "Nop.Plugin.Misc.PaymentGuard.Tasks.MonitoringTask",
            //        Enabled = true,
            //        StopOnError = false,
            //    });
            //}

            // Install cleanup task
            if (await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardCleanupTask") == null)
            {
                await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
                {
                    Name = "PaymentGuard Cleanup Task",
                    Seconds = 86400, // Daily (24 hours * 60 minutes * 60 seconds)
                    Type = "Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardCleanupTask",
                    Enabled = true,
                    StopOnError = false,
                });
            }

            // Install maintenance task for script verification
            if (await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardMaintenanceTask") == null)
            {
                await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
                {
                    Name = "PaymentGuard Script Verification Task",
                    Seconds = 604800, // Weekly
                    Type = "Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardMaintenanceTask",
                    Enabled = true,
                    StopOnError = false,
                });
            }

            // Install report task for weekly report
            if (await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.WeeklyComplianceReportTask") == null)
            {
                await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
                {
                    Name = "PaymentGuard Weekly Compliance Reports",
                    Type = "Nop.Plugin.Misc.PaymentGuard.Tasks.WeeklyComplianceReportTask, Nop.Plugin.Misc.PaymentGuard",
                    Enabled = true,
                    StopOnError = false,
                    Seconds = 7 * 24 * 60 * 60 // Run weekly (7 days in seconds)
                });
            }
        }

        private async Task UninstallScheduleTaskAsync()
        {
            var task = await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.MonitoringTask");
            if (task != null)
                await _scheduleTaskService.DeleteTaskAsync(task);

            task = await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardCleanupTask");
            if (task != null)
                await _scheduleTaskService.DeleteTaskAsync(task);

            task = await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardMaintenanceTask");
            if (task != null)
                await _scheduleTaskService.DeleteTaskAsync(task);

            task = await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.WeeklyComplianceReportTask");
            if (task != null)
                await _scheduleTaskService.DeleteTaskAsync(task);
        }

        private async Task InstallMessageTemplatesAsync()
        {
            var templates = new[]
            {
                // 1. Unauthorized Scripts Alert Template
                new MessageTemplate
                {
                    Name = "PaymentGuard.UnauthorizedScriptsAlert",
                    Subject = "PaymentGuard Alert - Unauthorized Scripts Detected (%Store.Name%)",
                    Body = @"<h2>PaymentGuard Security Alert</h2>
                            <p><strong>Store:</strong> %Store.Name%</p>
                            <p><strong>Page:</strong> %PaymentGuard.PageUrl%</p>
                            <p><strong>Time:</strong> %PaymentGuard.CheckedTime%</p>

                            <h3>Unauthorized Scripts Detected:</h3>
                            <ul>
                                %PaymentGuard.UnauthorizedScriptsList%
                            </ul>

                            <h3>Summary:</h3>
                            <ul>
                                <li>Total scripts found: %PaymentGuard.TotalScriptsFound%</li>
                                <li>Authorized scripts: %PaymentGuard.AuthorizedScriptsCount%</li>
                                <li>Unauthorized scripts: %PaymentGuard.UnauthorizedScriptsCount%</li>
                            </ul>

                            <p><strong>Action Required:</strong> Please review these unauthorized scripts and either authorize them or remove them from your payment pages to maintain PCI DSS compliance.</p>

                            <hr>
                            <small>This alert was generated by PaymentGuard PCI DSS Compliance System</small>",
                    IsActive = true,
                    EmailAccountId = 0
                },

                // 2. Compliance Report Template
                new MessageTemplate
                {
                    Name = "PaymentGuard.ComplianceReport",
                    Subject = "PaymentGuard Compliance Report (%Store.Name%)",
                    Body = @"<h2>PaymentGuard Compliance Report</h2>
                        <p><strong>Store:</strong> %Store.Name%</p>
                        <p><strong>Report Generated:</strong> %PaymentGuard.ReportGeneratedTime%</p>

                        <h3>Compliance Score: %PaymentGuard.ComplianceScore%%</h3>

                        <h3>Summary:</h3>
                        <table border='1' cellpadding='5' cellspacing='0'>
                            <tr><td>Total Scripts Monitored</td><td>%PaymentGuard.TotalScriptsMonitored%</td></tr>
                            <tr><td>Authorized Scripts</td><td>%PaymentGuard.AuthorizedScriptsCount%</td></tr>
                            <tr><td>Unauthorized Scripts</td><td>%PaymentGuard.UnauthorizedScriptsCount%</td></tr>
                            <tr><td>Total Checks Performed</td><td>%PaymentGuard.TotalChecksPerformed%</td></tr>
                            <tr><td>Alerts Generated</td><td>%PaymentGuard.AlertsGenerated%</td></tr>
                            <tr><td>Last Check</td><td>%PaymentGuard.LastCheckDate%</td></tr>
                        </table>

                        %PaymentGuard.MostCommonUnauthorizedScriptsSection%

                        <hr>
                        <small>This report was generated by PaymentGuard PCI DSS Compliance System</small>",
                    IsActive = true,
                    EmailAccountId = 0
                },

                // 3. Script Change Alert Template
                new MessageTemplate
                {
                    Name = "PaymentGuard.ScriptChangeAlert",
                    Subject = "PaymentGuard Alert - Script Change Detected (%Store.Name%)",
                    Body = @"<h2>PaymentGuard Script Change Alert</h2>
                        <p><strong>Store:</strong> %Store.Name%</p>
                        <p><strong>Changed Script:</strong> <code>%PaymentGuard.ScriptUrl%</code></p>
                        <p><strong>Time:</strong> %PaymentGuard.AlertTime%</p>

                        <p><strong>Action Required:</strong> A previously authorized script has been modified. Please verify this change is legitimate and update the script hash if necessary.</p>

                        <hr>
                        <small>This alert was generated by PaymentGuard PCI DSS Compliance System</small>",
                    IsActive = true,
                    EmailAccountId = 0
                },

                // 4. CSP Violation Alert Template
                new MessageTemplate
                {
                    Name = "PaymentGuard.CSPViolationAlert",
                    Subject = "PaymentGuard Alert - CSP Violation (%Store.Name%)",
                    Body = @"<h2>PaymentGuard CSP Violation Alert</h2>
                        <p><strong>Store:</strong> %Store.Name%</p>
                        <p><strong>Time:</strong> %PaymentGuard.AlertTime%</p>

                        <h3>Violation Details:</h3>
                        <pre>%PaymentGuard.ViolationDetails%</pre>

                        <p><strong>Action Required:</strong> A Content Security Policy violation has been detected. Please review your CSP configuration and authorized scripts.</p>

                        <hr>
                        <small>This alert was generated by PaymentGuard PCI DSS Compliance System</small>",
                    IsActive = true,
                    EmailAccountId = 0
                },

                // 5. Expired Scripts Alert Template
                new MessageTemplate
                {
                    Name = "PaymentGuard.ExpiredScriptsAlert",
                    Subject = "PaymentGuard Alert - Scripts Need Verification (%Store.Name%)",
                    Body = @"<h2>PaymentGuard Script Verification Alert</h2>
                        <p><strong>Store:</strong> %Store.Name%</p>
                        <p><strong>Time:</strong> %PaymentGuard.AlertTime%</p>

                        <h3>Scripts Requiring Verification:</h3>
                        <ul>%PaymentGuard.ExpiredScriptsList%</ul>

                        <p><strong>Action Done:</strong> These scripts weren't been verified in last 30 days. Their hashes updated now, please verify once if it's need your review.</p>

                        <hr>
                        <small>This alert was generated by PaymentGuard PCI DSS Compliance System</small>",
                    IsActive = true,
                    EmailAccountId = 0
                },

                // 6. Blocked Script Alert Template
                new MessageTemplate
                {
                    Name = "PaymentGuard.BlockedScriptAlert",
                    Subject = "PaymentGuard Security Alert - Script Blocked",
                    Body = @"<h2 style='color: #dc3545;'>Security Alert: Script Blocked</h2>

                        <p><strong>A potentially unsafe script has been blocked from executing on your payment page.</strong></p>

                        <table style='border-collapse: collapse; width: 100%;'>
                            <tr>
                                <td style='padding: 8px; border: 1px solid #ddd; font-weight: bold;'>Blocked Script:</td>
                                <td style='padding: 8px; border: 1px solid #ddd;'>%PaymentGuard.ScriptUrl%</td>
                            </tr>
                            <tr>
                                <td style='padding: 8px; border: 1px solid #ddd; font-weight: bold;'>Page URL:</td>
                                <td style='padding: 8px; border: 1px solid #ddd;'>%PaymentGuard.PageUrl%</td>
                            </tr>
                            <tr>
                                <td style='padding: 8px; border: 1px solid #ddd; font-weight: bold;'>Timestamp:</td>
                                <td style='padding: 8px; border: 1px solid #ddd;'>%PaymentGuard.AlertTime%</td>
                            </tr>
                            <tr>
                                <td style='padding: 8px; border: 1px solid #ddd; font-weight: bold;'>Reason:</td>
                                <td style='padding: 8px; border: 1px solid #ddd;'>Script lacks SRI (Subresource Integrity) validation</td>
                            </tr>
                        </table>

                        <h3 style='color: #dc3545; margin-top: 20px;'>Required Actions:</h3>
                        <ol>
                            <li>Verify if this script is legitimate and required</li>
                            <li>If legitimate, add the script to authorized scripts with proper SRI hash</li>
                            <li>If unauthorized, investigate how it was added to the page</li>
                            <li>Review payment page security immediately</li>
                        </ol>

                        <p style='margin-top: 20px;'><strong>This is an automated security alert from PaymentGuard PCI DSS Compliance Plugin.</strong></p>",
                    IsActive = true,
                    EmailAccountId = 0
                }
            };
        }

        // Also add this method to uninstall templates when plugin is uninstalled
        private async Task UninstallMessageTemplatesAsync()
        {
            var templateNames = new[]
            {
                "PaymentGuard.UnauthorizedScriptsAlert",
                "PaymentGuard.ComplianceReport",
                "PaymentGuard.ScriptChangeAlert",
                "PaymentGuard.CSPViolationAlert",
                "PaymentGuard.ExpiredScriptsAlert",
                "PaymentGuard.BlockedScriptAlert"
            };

            foreach (var templateName in templateNames)
            {
                var templates = await _messageTemplateService.GetMessageTemplatesByNameAsync(templateName);
                if (templates != null)
                {
                    foreach (var temp in templates)
                    {
                        await _messageTemplateService.DeleteMessageTemplateAsync(temp);
                    }
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets widget zones where this widget should be rendered
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the widget zones
        /// </returns>
        public Task<IList<string>> GetWidgetZonesAsync()
        {
            return Task.FromResult<IList<string>>(new List<string> { PublicWidgetZones.HeadHtmlTag, AdminWidgetZones.MessageTemplateDetailsBottom });
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        /// <returns></returns>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentGuard/Configure";
        }

        /// <summary>
        /// Gets a type of a view component for displaying widget
        /// </summary>
        /// <param name="widgetZone">Name of the widget zone</param>
        /// <returns>View component type</returns>
        public Type GetWidgetViewComponent(string widgetZone)
        {
            if (widgetZone == AdminWidgetZones.MessageTemplateDetailsBottom)
                return typeof(PaymentGuardMessageTemplateViewComponent);
            
            return typeof(PaymentGuardViewComponent);
        }

        public async Task ManageSiteMapAsync(SiteMapNode rootNode)
        {
            if (await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManagePaymentGuard))
            {
                var menuItem = new SiteMapNode()
                {
                    Title = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Menu"),
                    SystemName = "PaymentGuard.Menu",
                    Visible = true,
                    IconClass = "fas fa-shield-alt"
                };

                if (await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                {
                    menuItem.ChildNodes.Add(new SiteMapNode()
                    {
                        Title = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Menu.Configuration"),
                        SystemName = "PaymentGuard.Menu.Configure",
                        Url = "~/Admin/PaymentGuard/Configure",
                        Visible = true,
                        IconClass = "fas fa-cog"
                    });
                }

                menuItem.ChildNodes.Add(new SiteMapNode()
                {
                    Title = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Menu.ScriptManagement"),
                    SystemName = "PaymentGuard.Menu.ScriptManagement",
                    Url = "~/Admin/PaymentGuard/List",
                    Visible = true,
                    IconClass = "fas fa-code"
                });

                if (await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                {
                    menuItem.ChildNodes.Add(new SiteMapNode()
                    {
                        Title = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Menu.Dashboard"),
                        SystemName = "PaymentGuard.Menu.Dashboard",
                        Url = "~/Admin/PaymentGuard/Dashboard",
                        Visible = true,
                        IconClass = "fas fa-chart-line"
                    });

                    menuItem.ChildNodes.Add(new SiteMapNode()
                    {
                        Title = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Menu.ComplianceReport"),
                        SystemName = "PaymentGuard.Menu.ComplianceReport",
                        Url = "~/Admin/PaymentGuard/GenerateComplianceReport",
                        Visible = true,
                        IconClass = "fas fa-clipboard-list"
                    });
                }

                if (await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                {
                    menuItem.ChildNodes.Add(new SiteMapNode()
                    {
                        Title = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Menu.Alerts"),
                        SystemName = "PaymentGuard.Menu.Alerts",
                        Url = "~/Admin/ComplianceAlert/List",
                        Visible = true,
                        IconClass = "fas fa-bell"
                    });
                }

                var pluginNode = rootNode.ChildNodes.FirstOrDefault(x => x.SystemName == "Third party plugins");
                if (pluginNode != null)
                    pluginNode.ChildNodes.Add(menuItem);
                else
                    rootNode.ChildNodes.Add(menuItem);
            }
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// </returns>
        public override async Task InstallAsync()
        {
            // Install default settings
            await _settingService.SaveSettingAsync(new PaymentGuardSettings
            {
                IsEnabled = true,
                EnableEmailAlerts = true,
                EnableCSPHeaders = true,
                EnableSRIValidation = true,
                CSPPolicy = "script-src 'self' 'unsafe-inline';",
                EnableDetailedLogging = true,
                MonitoredPages = "/checkout,/onepagecheckout",
                MaxAlertFrequency = 24,
                LogRetentionDays = 90,
                AlertRetentionDays = 30,
                EnableAutomaticCleanup = true,
                CacheExpirationMinutes = 60,
                EnableApiRateLimit = true,
                ApiRateLimitPerHour = 1000,
                WhitelistedIPs = "",
                TrustedDomains = "cdnjs.cloudflare.com,cdn.jsdelivr.net,code.jquery.com,stackpath.bootstrapcdn.com,ajax.googleapis.com,maxcdn.bootstrapcdn.com",
                PaymentProviders = "stripe,paypal,square,braintree,razorpay,cardknox,authorize.net,payoneer,adyen"
            });

            if (!_widgetSettings.ActiveWidgetSystemNames.Contains(PaymentGuardDefaults.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Add(PaymentGuardDefaults.SystemName);
                await _settingService.SaveSettingAsync(_widgetSettings);
            }

            // Install scheduled tasks
            await InstallScheduleTaskAsync();

            // Install permissions
            await InstallPermissionsAsync();

            await InstallMessageTemplatesAsync();

            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                // Plugin Info
                ["Plugins.Misc.PaymentGuard.FriendlyName"] = "PaymentGuard - PCI DSS Compliance",
                ["Plugins.Misc.PaymentGuard.Description"] = "PCI DSS v4.0 compliance solution for payment page script monitoring and security",
                ["Plugins.Misc.PaymentGuard.Refresh"] = "Refresh",

                // Menu Items
                ["Plugins.Misc.PaymentGuard.Menu"] = "Payment Guard",
                ["Plugins.Misc.PaymentGuard.Menu.ScriptManagement"] = "Script Management",
                ["Plugins.Misc.PaymentGuard.Menu.Dashboard"] = "Monitoring Dashboard",
                ["Plugins.Misc.PaymentGuard.Menu.ComplianceReport"] = "Compliance Report",
                ["Plugins.Misc.PaymentGuard.Menu.Configuration"] = "Configuration",
                ["Plugins.Misc.PaymentGuard.Menu.Alerts"] = "Compliance Alerts",
                ["Plugins.Misc.PaymentGuard.Menu.MonitoringLogs"] = "Monitoring Logs",

                // Page Titles
                ["Plugins.Misc.PaymentGuard.AddNewScript"] = "Add New Authorized Script",
                ["Plugins.Misc.PaymentGuard.EditScript"] = "Edit Authorized Script",
                ["Plugins.Misc.PaymentGuard.BackToList"] = "back to script list",
                ["Plugins.Misc.PaymentGuard.ManageScripts"] = "Manage Scripts",
                ["Plugins.Misc.PaymentGuard.Alerts.List"] = "Compliance Alerts",
                ["Plugins.Misc.PaymentGuard.Alerts.Details"] = "Alert Details",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.List"] = "Monitoring Logs",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Details"] = "Log Details",
                ["Plugins.Misc.PaymentGuard.BackToAlerts"] = "back to alerts list",
                ["Plugins.Misc.PaymentGuard.BackToLogs"] = "back to monitoring logs",

                // Configuration Sections
                ["Plugins.Misc.PaymentGuard.Configuration.GeneralSettings"] = "General Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.AlertSettings"] = "Alert Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.SecuritySettings"] = "Security Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.MaintenanceSettings"] = "Maintenance Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.MaintenanceSettings.Help"] = "Configure automatic cleanup and caching behavior",
                ["Plugins.Misc.PaymentGuard.Configuration.ApiSettings"] = "API Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.ApiSettings.Help"] = "Configure API rate limiting and security settings",
                ["Plugins.Misc.PaymentGuard.Configuration.PaymentDetection"] = "Payment Detection Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.ScheduledReports"] = "Scheduled Reports",

                // Configuration Fields
                ["Plugins.Misc.PaymentGuard.Configure.Fields.IsEnabled"] = "Enabled",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.IsEnabled.Hint"] = "Enable or disable PaymentGuard monitoring for this store",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.EnableEmailAlerts"] = "Enable Email Alerts",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.EnableEmailAlerts.Hint"] = "Send email notifications when unauthorized scripts are detected",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.EnableCSPHeaders"] = "Enable CSP Headers",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.EnableCSPHeaders.Hint"] = "Automatically inject Content Security Policy headers on monitored pages",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.EnableSRIValidation"] = "Enable SRI Validation",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.EnableSRIValidation.Hint"] = "Enable Subresource Integrity validation for external scripts",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.CSPPolicy"] = "Content Security Policy",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.CSPPolicy.Hint"] = "Content Security Policy directives. Use 'self' for same-origin scripts.",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.MonitoredPages"] = "Monitored Pages",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.MonitoredPages.Hint"] = "Comma-separated list of pages to monitor (e.g., /checkout,/onepagecheckout)",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.MaxAlertFrequency"] = "Max Alert Frequency (hours)",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.MaxAlertFrequency.Hint"] = "Minimum hours between duplicate alerts",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.LogRetentionDays"] = "Log Retention (days)",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.LogRetentionDays.Hint"] = "Number of days to retain monitoring logs",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.AlertRetentionDays"] = "Alert Retention (days)",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.AlertRetentionDays.Hint"] = "Number of days to retain resolved alerts",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.EnableAutomaticCleanup"] = "Enable Automatic Cleanup",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.EnableAutomaticCleanup.Hint"] = "Automatically clean up old logs and alerts",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.ApiRateLimitPerHour"] = "API Rate Limit (per hour)",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.ApiRateLimitPerHour.Hint"] = "Maximum API calls per hour per IP address",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.WhitelistedIPs"] = "Whitelisted IPs",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.WhitelistedIPs.Hint"] = "Comma-separated list of IP addresses that bypass rate limiting",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.CacheExpirationMinutes"] = "Cache Expiration (minutes)",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.CacheExpirationMinutes.Hint"] = "How long to cache script authorization lookups (recommended: 30-120 minutes)",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.TrustedDomains"] = "Trusted Domains",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.TrustedDomains.Hint"] = "Comma-separated list of trusted CDN domains for auto-hash updates (e.g., cdnjs.cloudflare.com,cdn.jsdelivr.net)",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.PaymentProviders"] = "Payment Providers",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.PaymentProviders.Hint"] = "Comma-separated list of payment provider keywords for script detection (e.g., stripe,paypal,square)",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.SendWeeklyReports"] = "Send Weekly Reports",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.SendWeeklyReports.Hint"] = "Automatically send weekly compliance reports to the alert email address.",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.WeeklyReportDay"] = "Weekly Report Day",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.WeeklyReportDay.Hint"] = "Day of the week to send weekly compliance reports.",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.LastWeeklyReportSent"] = "Last Weekly Report Sent",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.LastWeeklyReportSent.Hint"] = "Last weekly report sent date.",

                // Validation Messages
                ["Plugins.Misc.PaymentGuard.Configure.Fields.MaxAlertFrequency.Range"] = "Max Alert Frequency must be between 1 and 168 hours",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.LogRetentionDays.Range"] = "Log retention must be between 1 and 365 days",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.AlertRetentionDays.Range"] = "Alert retention must be between 1 and 365 days",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.CacheExpirationMinutes.Range"] = "Cache expiration must be between 1 and 1440 minutes (24 hours)",
                ["Plugins.Misc.PaymentGuard.Configure.Fields.ApiRateLimitPerHour.Range"] = "API rate limit must be between 1 and 100000 requests per hour",


                // Script Fields
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchScriptUrl"] = "Search Script URL",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchScriptUrl.Hint"] = "Enter script URL to search for",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchIsActive"] = "Search Active Status",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchIsActive.Hint"] = "Filter by active/inactive status",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchRiskLevel"] = "Search Risk Level",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchRiskLevel.Hint"] = "Filter by risk level (Low, Medium, High)",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchSource"] = "Search Source Type",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchSource.Hint"] = "Filter by source type (Internal, Third-party, Payment Gateway, etc.)",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchStore"] = "Search Store",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchStore.Hint"] = "Filter by store",

                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptUrl"] = "Script URL",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptUrl.Hint"] = "Full URL of the JavaScript file (e.g., https://example.com/script.js)",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptUrl.Required"] = "Script URL of the JavaScript file required.",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptUrl.InvalidFormat"] = "Please enter a valid URL format (e.g., https://example.com/script.js).",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptUrl.MustBeHttps"] = "External script URLs must use HTTPS for security compliance.",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Purpose"] = "Purpose",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Purpose.Hint"] = "Brief description of what this script does",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Purpose.Required"] = "Brief description of file is required.",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Justification"] = "Business Justification",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Justification.Hint"] = "Business justification for why this script is necessary (required for PCI DSS compliance)",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Justification.Required"] = "Business justification of file is required.",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.GenerateHash"] = "Generate SRI Hash",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.GenerateHash.Hint"] = "Automatically generate SRI hash for this script",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptHash"] = "Script Hash",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptHash.Hint"] = "SHA-384 hash for Subresource Integrity validation",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.RiskLevel"] = "Risk Level",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.RiskLevel.Hint"] = "Security risk level of this script (Low, Medium, High)",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Source"] = "Source Type",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Source.Hint"] = "Where this script originates from (Internal, Third-party, Payment Gateway, etc.)",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Store"] = "Store",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Store.Hint"] = "Choose the store to authorized script for it.",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.IsActive"] = "Active",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.IsActive.Hint"] = "Whether this script is currently authorized for use",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.AuthorizedBy"] = "Authorized By",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.AuthorizedBy.Hint"] = "User who authorized this script",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.AuthorizedOnUtc"] = "Authorized On",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.AuthorizedOnUtc.Hint"] = "Date and time when this script was authorized",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.LastVerifiedUtc"] = "Last Verified",
                ["Plugins.Misc.PaymentGuard.ScriptManagement.Fields.LastVerifiedUtc.Hint"] = "Date and time when this script was last verified for integrity",


                // Compliance Report Resources  
                ["Plugins.Misc.PaymentGuard.ComplianceReport.PageTitle"] = "Compliance Report",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.DownloadPDF"] = "Download PDF",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.BackToDashboard"] = "Back to Dashboard",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.ReportPeriod"] = "Report Period:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Generated"] = "Generated:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.TotalScripts"] = "Total Scripts",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Authorized"] = "Authorized",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Unauthorized"] = "Unauthorized",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.ChecksPerformed"] = "Checks Performed",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.ComplianceAssessment"] = "Compliance Assessment",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Recommendations"] = "Recommendations",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.HighPriority"] = "High Priority",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.MediumPriority"] = "Medium Priority",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.LowPriority"] = "Low Priority",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.BestPractices"] = "Best Practices",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.MostCommonIssues"] = "Most Common Issues",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.AdditionalInformation"] = "Additional Information",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.LastCheckDate"] = "Last Check Date:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.TotalAlertsGenerated"] = "Total Alerts Generated:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.IssueRate"] = "Issue Rate:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.ReportType"] = "Report Type:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.RequirementsCovered"] = "Requirements Covered:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.NextRecommendedReview"] = "Next Recommended Review:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.PCIDSSCompliance"] = "PCI DSS Compliance Assessment",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Requirements"] = "6.4.3, 11.6.1",

                // Compliance Alert Fields
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.ScriptUrl"] = "Script URL",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.ScriptUrl.Hint"] = "Full URL of the JavaScript file (e.g., https://example.com/script.js)",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.AlertType"] = "Alert Type",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.AlertType.Hint"] = "The type of security alert",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.AlertLevel"] = "Alert Level",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.AlertLevel.Hint"] = "The severity level of the alert",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.Message"] = "Message",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.Message.Hint"] = "Brief description of the alert",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.Pageurl"] = "Page url",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.Pageurl.Hint"] = "Page url of script file from which got alert.",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.Details"] = "Details",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.Details.Hint"] = "Detailed information about the alert",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.IsResolved"] = "Resolved",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.IsResolved.Hint"] = "Whether this alert has been resolved",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.ResolvedBy"] = "Resolved By",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.ResolvedBy.Hint"] = "User who resolved this alert",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.ResolvedOnUtc"] = "Resolved On",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.ResolvedOnUtc.Hint"] = "Date and time when this alert was resolved",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.EmailSent"] = "Email Sent",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.EmailSent.Hint"] = "Whether an email notification was sent",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.EmailSentOnUtc"] = "Email Sent On",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.EmailSentOnUtc.Hint"] = "Date and time when email was sent",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.CreatedOnUtc"] = "Created On",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.CreatedOnUtc.Hint"] = "Date and time when this alert was created",

                // Search Fields for Alerts
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.SearchAlertType"] = "Alert Type",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.SearchAlertType.Hint"] = "Filter by alert type",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.SearchAlertLevel"] = "Alert Level",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.SearchAlertLevel.Hint"] = "Filter by alert severity level",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.SearchIsResolved"] = "Resolution Status",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.SearchIsResolved.Hint"] = "Filter by resolution status",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.SearchStoreId"] = "Search Store",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Fields.SearchStoreId.Hint"] = "Filter by store",
                
                // ComplianceAlert Details View
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Details.PageTitle"] = "Compliance Alert Details",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Details.AlertOverview"] = "Alert Overview",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Details.TechnicalDetails"] = "Technical Details",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Details.RecommendedActions"] = "Recommended Actions",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.BackToList"] = "back to alerts list",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.ResolveAlert"] = "Resolve Alert",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.EmailSent"] = "Sent",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.EmailNotSent"] = "Not Sent",

                // Alert Actions
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.UnauthorizedScript.Title"] = "Unauthorized Script Detected",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.UnauthorizedScript.Action1"] = "Review the script to determine if it's legitimate",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.UnauthorizedScript.Action2"] = "If legitimate, add it to the authorized scripts list",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.UnauthorizedScript.Action3"] = "If malicious, investigate how it was added and remove it",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.AuthorizeScript"] = "Authorize This Script",

                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.CSPViolation.Title"] = "Content Security Policy Violation",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.CSPViolation.Action1"] = "Review the CSP policy configuration",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.CSPViolation.Action2"] = "Add legitimate script domains to the CSP policy",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.CSPViolation.Action3"] = "Remove or block unauthorized script sources",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.UpdateCSPPolicy"] = "Update CSP Policy",

                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.IntegrityFailure.Title"] = "Script Integrity Failure",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.IntegrityFailure.Action1"] = "Verify if the script has been legitimately updated",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.IntegrityFailure.Action2"] = "Update the SRI hash if the script change is authorized",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.IntegrityFailure.Action3"] = "Investigate potential tampering if change is unauthorized",

                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.General.Title"] = "Security Alert",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.General.Action1"] = "Review the alert details and take appropriate action",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Actions.General.Action2"] = "Update your security configuration if needed",

                // Alert Confirmation & Error Messages
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Confirm.Resolve"] = "Are you sure you want to resolve this alert?",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Error.ResolveFailed"] = "Failed to resolve alert. Please try again.",
                ["Plugins.Misc.PaymentGuard.ComplianceAlert.Error.DeleteFailed"] = "Failed to delete alert. Please try again.",

                // Compliance Report Fields
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Fields.SearchDateFrom"] = "Date From",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Fields.SearchDateFrom.Hint"] = "Filter from this date",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Fields.SearchDateTo"] = "Date To",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Fields.SearchDateTo.Hint"] = "Filter to this date",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Fields.SearchStore"] = "Search Store",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Fields.SearchStore.Hint"] = "Filter by store",

                // Monitoring Log Fields
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.View"] = "View",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Compliant"] = "Compliant",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.HasIssues"] = "Has Issues",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Manual"] = "Manual",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Scheduled"] = "Scheduled",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.ManualCheckUrl"] = "Page URL:",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.UrlPlaceholder"] = "https://yourstore.com/checkout",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Cancel"] = "Cancel",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.RunCheck"] = "Run Check",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.EnterValidUrl"] = "Please enter a valid URL",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.ErrorPerformingCheck"] = "Error performing manual check",

                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.PageUrl"] = "Page URL",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.PageUrl.Hint"] = "The URL of the monitored page",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.TotalScriptsFound"] = "Total Scripts",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.TotalScriptsFound.Hint"] = "Total number of scripts found",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.AuthorizedScriptsCount"] = "Authorized Scripts",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.AuthorizedScriptsCount.Hint"] = "Number of authorized scripts",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.UnauthorizedScriptsCount"] = "Unauthorized Scripts",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.UnauthorizedScriptsCount.Hint"] = "Number of unauthorized scripts",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.HasUnauthorizedScripts"] = "Has Issues",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.HasUnauthorizedScripts.Hint"] = "Whether unauthorized scripts were detected",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.CheckedOnUtc"] = "Checked On",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.CheckedOnUtc.Hint"] = "Date and time when check was performed",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.CheckType"] = "Check Type",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.CheckType.Hint"] = "Type of monitoring check",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.AlertSent"] = "Alert Sent",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.AlertSent.Hint"] = "Whether an alert was sent",

                // Search Fields for Monitoring Logs
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchPageUrl"] = "Page URL",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchPageUrl.Hint"] = "Filter by page URL",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchHasUnauthorizedScripts"] = "Compliance Status",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchHasUnauthorizedScripts.Hint"] = "Filter by compliance status",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchDateFrom"] = "Date From",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchDateFrom.Hint"] = "Filter from this date",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchDateTo"] = "Date To",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchDateTo.Hint"] = "Filter to this date",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchStoreId"] = "Search Store",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchStoreId.Hint"] = "Filter by store",

                // Dashboard Labels
                ["Plugins.Misc.PaymentGuard.Dashboard.PageTitle"] = "PaymentGuard Dashboard",
                ["Plugins.Misc.PaymentGuard.Dashboard.TotalScriptsMonitoredLabel"] = "Total Scripts Monitored",
                ["Plugins.Misc.PaymentGuard.Dashboard.AuthorizedScriptsLabel"] = "Authorized Scripts",
                ["Plugins.Misc.PaymentGuard.Dashboard.UnauthorizedScriptsLabel"] = "Unauthorized Scripts",
                
                ["Plugins.Misc.PaymentGuard.Dashboard.ScriptsVerificationLabel"] = "Scripts Need Verification",
                ["Plugins.Misc.PaymentGuard.Dashboard.ScriptsVerificationDetail"] = "You have <strong>{0}</strong> authorized scripts that haven't been verified in over 30 days.",
                ["Plugins.Misc.PaymentGuard.Dashboard.ScriptsVerificationDaysDue"] = "days overdue",
                ["Plugins.Misc.PaymentGuard.Dashboard.ScriptsVerificationReview"] = "Review Scripts",

                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceScoreLabel"] = "Compliance Score",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceOverview"] = "Compliance Overview",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceOverview.Score"] = "Compliance Score:",
                ["Plugins.Misc.PaymentGuard.Dashboard.RecentActivity"] = "Recent Activity",
                ["Plugins.Misc.PaymentGuard.Dashboard.Authorized"] = "AUTHORIZED",
                ["Plugins.Misc.PaymentGuard.Dashboard.Unauthorized"] = "UNAUTHORIZED",
                ["Plugins.Misc.PaymentGuard.Dashboard.Checks"] = "CHECKS",
                ["Plugins.Misc.PaymentGuard.Dashboard.LastMonitoringCheck"] = "Last monitoring check:",
                ["Plugins.Misc.PaymentGuard.Dashboard.TotalAlertsGenerated"] = "Total alerts generated:",
                ["Plugins.Misc.PaymentGuard.Dashboard.ScriptsUnderProtection"] = "Scripts under protection:",
                ["Plugins.Misc.PaymentGuard.Dashboard.MostCommonUnauthorizedScripts"] = "Most Common Unauthorized Scripts:",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceExcellent"] = "Excellent",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceGood"] = "Good",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceNeedsAttention"] = "Needs Attention",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceTrends"] = "Compliance Trends (Last 30 Days)",
                ["Plugins.Misc.PaymentGuard.Dashboard.AlertTypesDistribution"] = "Alert Types Distribution",
                ["Plugins.Misc.PaymentGuard.Dashboard.MonitoringActivity"] = "Monitoring Activity",
                ["Plugins.Misc.PaymentGuard.Dashboard.RiskLevelBreakdown"] = "Risk Level Breakdown",
                ["Plugins.Misc.PaymentGuard.Dashboard.PerformanceMetrics"] = "Performance Metrics",
                ["Plugins.Misc.PaymentGuard.Dashboard.TopViolatingScripts"] = "Top Violating Scripts",
                ["Plugins.Misc.PaymentGuard.Dashboard.AvgResponseTime"] = "Avg Response Time",
                ["Plugins.Misc.PaymentGuard.Dashboard.SystemUptime"] = "System Uptime",
                ["Plugins.Misc.PaymentGuard.Dashboard.CacheHitRate"] = "Cache Hit Rate",
                ["Plugins.Misc.PaymentGuard.Dashboard.ApiCallsThisWeek"] = "API Calls This Week",
                ["Plugins.Misc.PaymentGuard.Dashboard.SuccessRate"] = "Success Rate",
                ["Plugins.Misc.PaymentGuard.Dashboard.AvgResolutionTime"] = "Avg Resolution Time",
                ["Plugins.Misc.PaymentGuard.Dashboard.ResolvedThisWeek"] = "Resolved This Week",
                ["Plugins.Misc.PaymentGuard.Dashboard.NewAlerts"] = "New Alerts",
                ["Plugins.Misc.PaymentGuard.Dashboard.SecurityPosture"] = "Security Posture",
                ["Plugins.Misc.PaymentGuard.Dashboard.Script"] = "Script",
                ["Plugins.Misc.PaymentGuard.Dashboard.Violations"] = "Violations",
                ["Plugins.Misc.PaymentGuard.Dashboard.Risk"] = "Risk",
                ["Plugins.Misc.PaymentGuard.Dashboard.LastSeen"] = "Last Seen",
                ["Plugins.Misc.PaymentGuard.Dashboard.NoViolationsDetected"] = "No violations detected",
                ["Plugins.Misc.PaymentGuard.Dashboard.Refresh"] = "Refresh",
                ["Plugins.Misc.PaymentGuard.Dashboard.Loading"] = "Loading...",
                ["Plugins.Misc.PaymentGuard.Dashboard.SecureCompliant"] = "Secure & Compliant",
                ["Plugins.Misc.PaymentGuard.Dashboard.NewThisWeek"] = "new this week",
                ["Plugins.Misc.PaymentGuard.Dashboard.VsLastWeek"] = "vs last week",
                ["Plugins.Misc.PaymentGuard.Dashboard.NoAlertsInSelectedPeriod"] = "No alerts in selected period",
                ["Plugins.Misc.PaymentGuard.Dashboard.Last7Days"] = "Last 7 Days",
                ["Plugins.Misc.PaymentGuard.Dashboard.Last30Days"] = "Last 30 Days",
                ["Plugins.Misc.PaymentGuard.Dashboard.Last90Days"] = "Last 90 Days",

                // Dashboard Alert-focused Labels
                ["Plugins.Misc.PaymentGuard.Dashboard.ActiveAlertsLabel"] = "Active Alerts",
                ["Plugins.Misc.PaymentGuard.Dashboard.AlertsLast24Hours"] = "Alerts (24h)",
                ["Plugins.Misc.PaymentGuard.Dashboard.SystemStatus"] = "System Status",
                ["Plugins.Misc.PaymentGuard.Dashboard.CriticalAlertsDetected"] = "Critical Security Alerts Detected",
                ["Plugins.Misc.PaymentGuard.Dashboard.CriticalAlertsMessage"] = "There are {0} unresolved critical alerts that require immediate attention.",
                ["Plugins.Misc.PaymentGuard.Dashboard.ViewCriticalAlerts"] = "View Critical Alerts",
                ["Plugins.Misc.PaymentGuard.Dashboard.RealTimeStatus"] = "Real-Time Monitoring Status",
                ["Plugins.Misc.PaymentGuard.Dashboard.ActiveSessions"] = "Active Sessions",
                ["Plugins.Misc.PaymentGuard.Dashboard.AlertsLastHour"] = "Alerts Last Hour",
                ["Plugins.Misc.PaymentGuard.Dashboard.LastAlert"] = "Last Alert",
                ["Plugins.Misc.PaymentGuard.Dashboard.ResolutionRate"] = "Resolution Rate",
                ["Plugins.Misc.PaymentGuard.Dashboard.RecentAlerts"] = "Recent Alerts",
                ["Plugins.Misc.PaymentGuard.Dashboard.ViewAll"] = "View All",
                ["Plugins.Misc.PaymentGuard.Dashboard.NewToday"] = "new today",
                ["Plugins.Misc.PaymentGuard.Dashboard.NoNewAlerts"] = "No new alerts",
                ["Plugins.Misc.PaymentGuard.Dashboard.Resolved"] = "Resolved",
                ["Plugins.Misc.PaymentGuard.Dashboard.NoRecentAlerts"] = "No recent alerts - system is healthy!",
                ["Plugins.Misc.PaymentGuard.Dashboard.AlertTrends"] = "Alert Trends",
                ["Plugins.Misc.PaymentGuard.Dashboard.ResolutionPerformance"] = "Resolution Performance",
                ["Plugins.Misc.PaymentGuard.Dashboard.RefreshSuccess"] = "Dashboard refreshed successfully",
                ["Plugins.Misc.PaymentGuard.Dashboard.RefreshError"] = "Error refreshing dashboard data",

                // Action Messages
                ["Plugins.Misc.PaymentGuard.ScriptAdded"] = "Script has been added successfully",
                ["Plugins.Misc.PaymentGuard.ScriptUpdated"] = "Script has been updated successfully",
                ["Plugins.Misc.PaymentGuard.ScriptDeleted"] = "Script has been deleted successfully",
                ["Plugins.Misc.PaymentGuard.Alerts.Resolved"] = "Alert has been resolved successfully",
                ["Plugins.Misc.PaymentGuard.Alerts.AlreadyResolved"] = "Alert is already resolved",
                ["Plugins.Misc.PaymentGuard.Alerts.NotFound"] = "Alert not found",
                ["Plugins.Misc.PaymentGuard.Alerts.Deleted"] = "Alert has been deleted successfully",
                ["Plugins.Misc.PaymentGuard.MonitoringCheck.Completed"] = "Manual monitoring check completed",
                ["Plugins.Misc.PaymentGuard.MonitoringCheck.Failed"] = "Manual monitoring check failed",
                ["Plugins.Misc.PaymentGuard.Export.Success"] = "Data exported successfully",
                ["Plugins.Misc.PaymentGuard.Export.Failed"] = "Error exporting data",
                ["Plugins.Misc.PaymentGuard.BulkResolve.Success"] = "Successfully resolved selected alerts",
                ["Plugins.Misc.PaymentGuard.BulkDelete.Success"] = "Successfully deleted selected alerts",

                // Buttons and Actions
                ["Plugins.Misc.PaymentGuard.ResolveAlert"] = "Resolve Alert",
                ["Plugins.Misc.PaymentGuard.ViewDetails"] = "View Details",
                ["Plugins.Misc.PaymentGuard.RunManualCheck"] = "Run Manual Check",
                ["Plugins.Misc.PaymentGuard.RefreshAlerts"] = "Refresh Alerts",
                ["Plugins.Misc.PaymentGuard.ExportToCsv"] = "Export to CSV",
                ["Plugins.Misc.PaymentGuard.ExportToPdf"] = "Export to PDF",
                ["Plugins.Misc.PaymentGuard.GenerateReport"] = "Generate Report",
                ["Plugins.Misc.PaymentGuard.BulkResolve"] = "Bulk Resolve",
                ["Plugins.Misc.PaymentGuard.BulkDelete"] = "Bulk Delete",
                ["Plugins.Misc.PaymentGuard.SelectAll"] = "Select All",

                ["Plugins.Misc.PaymentGuard.BulkActions"] = "Bulk Actions",
                ["Plugins.Misc.PaymentGuard.SelectAlerts"] = "Please select at least one alert",
                ["Plugins.Misc.PaymentGuard.BulkResolve.Confirm"] = "Are you sure you want to resolve all selected alerts?",

                // Status Labels
                ["Plugins.Misc.PaymentGuard.Status.Resolved"] = "Resolved",
                ["Plugins.Misc.PaymentGuard.Status.Unresolved"] = "Unresolved",
                ["Plugins.Misc.PaymentGuard.Status.Compliant"] = "Compliant",
                ["Plugins.Misc.PaymentGuard.Status.HasIssues"] = "Has Issues",
                ["Plugins.Misc.PaymentGuard.AlertType.UnauthorizedScript"] = "Unauthorized Script",
                ["Plugins.Misc.PaymentGuard.AlertType.CSPViolation"] = "CSP Violation",
                ["Plugins.Misc.PaymentGuard.AlertType.IntegrityFailure"] = "Integrity Failure",
                ["Plugins.Misc.PaymentGuard.AlertLevel.Critical"] = "Critical",
                ["Plugins.Misc.PaymentGuard.AlertLevel.Warning"] = "Warning",
                ["Plugins.Misc.PaymentGuard.AlertLevel.Info"] = "Info"
            });

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// </returns>
        public override async Task UninstallAsync()
        {
            // Remove settings
            await _settingService.DeleteSettingAsync<PaymentGuardSettings>();

            if (_widgetSettings.ActiveWidgetSystemNames.Contains(PaymentGuardDefaults.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Remove(PaymentGuardDefaults.SystemName);
                await _settingService.SaveSettingAsync(_widgetSettings);
            }

            // Remove scheduled task
            await UninstallScheduleTaskAsync();
            
            // Remove message templates
            await UninstallMessageTemplatesAsync();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.PaymentGuard");

            await base.UninstallAsync();
        }

        #endregion

        /// <summary>
        /// Gets a value indicating whether to hide this plugin on the widget list page in the admin area
        /// </summary>
        public bool HideInWidgetList => false;
    }
}