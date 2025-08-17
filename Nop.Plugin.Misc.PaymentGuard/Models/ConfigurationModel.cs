using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record ConfigurationModel : BaseNopModel, ISettingsModel
    {
        public ConfigurationModel()
        {
            WeeklyReportDayOptions = new List<SelectListItem>();
        }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.IsEnabled")]
        public bool IsEnabled { get; set; }
        public bool IsEnabled_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.EnableEmailAlerts")]
        public bool EnableEmailAlerts { get; set; }
        public bool EnableEmailAlerts_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.EnableCSPHeaders")]
        public bool EnableCSPHeaders { get; set; }
        public bool EnableCSPHeaders_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.EnableSRIValidation")]
        public bool EnableSRIValidation { get; set; }
        public bool EnableSRIValidation_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.CSPPolicy")]
        public string CSPPolicy { get; set; }
        public bool CSPPolicy_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.EnableDetailedLogging")]
        public bool EnableDetailedLogging { get; set; }
        public bool EnableDetailedLogging_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.MonitoredPages")]
        public string MonitoredPages { get; set; }
        public bool MonitoredPages_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.MaxAlertFrequency")]
        public int MaxAlertFrequency { get; set; }
        public bool MaxAlertFrequency_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.LogRetentionDays")]
        public int LogRetentionDays { get; set; }
        public bool LogRetentionDays_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.AlertRetentionDays")]
        public int AlertRetentionDays { get; set; }
        public bool AlertRetentionDays_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.EnableAutomaticCleanup")]
        public bool EnableAutomaticCleanup { get; set; }
        public bool EnableAutomaticCleanup_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.CacheExpirationMinutes")]
        public int CacheExpirationMinutes { get; set; }
        public bool CacheExpirationMinutes_OverrideForStore { get; set; }

        // New API Settings
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.EnableApiRateLimit")]
        public bool EnableApiRateLimit { get; set; }
        public bool EnableApiRateLimit_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.ApiRateLimitPerHour")]
        public int ApiRateLimitPerHour { get; set; }
        public bool ApiRateLimitPerHour_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.WhitelistedIPs")]
        public string WhitelistedIPs { get; set; }
        public bool WhitelistedIPs_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.TrustedDomains")]
        public string TrustedDomains { get; set; }
        public bool TrustedDomains_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.PaymentProviders")]
        public string PaymentProviders { get; set; }
        public bool PaymentProviders_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.SendWeeklyReports")]
        public bool SendWeeklyReports { get; set; }
        public bool SendWeeklyReports_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.WeeklyReportDay")]
        public int WeeklyReportDay { get; set; }
        public bool WeeklyReportDay_OverrideForStore { get; set; }

        public IList<SelectListItem> WeeklyReportDayOptions { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Configure.Fields.LastWeeklyReportSent")]
        public string LastWeeklyReportSent { get; set; }

        public int ActiveStoreScopeConfiguration { get; set; }
    }
}