using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Misc.PaymentGuard.Helpers;
using Nop.Plugin.Misc.PaymentGuard.Models;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Misc.PaymentGuard.Components
{
    public class PaymentGuardViewComponent : NopViewComponent
    {
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IAuthorizedScriptService _authorizedScriptService;
        private readonly ILogger _logger;
        private readonly SRIHelper _sriHelper;

        public PaymentGuardViewComponent(ISettingService settingService,
            IStoreContext storeContext,
            IWebHelper webHelper,
            IAuthorizedScriptService authorizedScriptService,
            ILogger logger,
            SRIHelper sriHelper)
        {
            _settingService = settingService;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _authorizedScriptService = authorizedScriptService;
            _logger = logger;
            _sriHelper = sriHelper;
        }

        #region Utilities

        private async Task<string> GenerateDynamicCSPPolicyAsync(int storeId, string basePolicy)
        {
            try
            {
                // Get all authorized external scripts
                var authorizedScripts = await _authorizedScriptService.GetAllAuthorizedScriptsAsync(
                    storeId: storeId,
                    isActive: true);

                // Extract unique external domains
                var externalDomains = new HashSet<string>();

                foreach (var script in authorizedScripts.Where(s => s.ScriptUrl.StartsWith("http")))
                {
                    try
                    {
                        var uri = new Uri(script.ScriptUrl);
                        var domain = $"{uri.Scheme}://{uri.Host}";
                        externalDomains.Add(domain);
                    }
                    catch
                    {
                        // Skip invalid URLs
                    }
                }

                // Build dynamic CSP policy
                if (externalDomains.Any())
                {
                    var allowedDomains = string.Join(" ", externalDomains);

                    // If base policy already contains script-src, append domains
                    if (basePolicy.Contains("script-src"))
                    {
                        return basePolicy.Replace("script-src", $"script-src {allowedDomains}").Replace(";;", ";");
                    }
                    else
                    {
                        // Add script-src directive
                        return $"{basePolicy}; script-src 'self' 'unsafe-inline' {allowedDomains};".Replace(";;", ";");
                    }
                }

                return basePolicy;
            }
            catch (Exception ex)
            {
                // Log error but don't break the page
                await _logger.ErrorAsync("Error generating dynamic CSP policy", ex);
                return basePolicy;
            }
        }

        #endregion

        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData = null)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var paymentGuardSettings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(store.Id);

            if (!paymentGuardSettings.IsEnabled)
                return Content("");

            // Only inject on monitored pages
            var currentPage = _webHelper.GetThisPageUrl(false);
            var monitoredPages = paymentGuardSettings.MonitoredPages?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                ?? new[] { "/checkout", "/onepagecheckout" };

            var isMonitoredPage = monitoredPages.Any(page =>
                currentPage.Contains(page.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!isMonitoredPage)
                return Content("");

            // NEW: Generate dynamic CSP policy based on authorized scripts
            string dynamicCSPPolicy = paymentGuardSettings.CSPPolicy;
            if (paymentGuardSettings.EnableCSPHeaders)
            {
                dynamicCSPPolicy = await GenerateDynamicCSPPolicyAsync(store.Id, paymentGuardSettings.CSPPolicy);
            }

            var model = new PaymentGuardScriptModel
            {
                IsEnabled = paymentGuardSettings.IsEnabled,
                StoreId = store.Id,
                ApiEndpoint = "/Plugins/PaymentGuard/Api",
                CSPPolicy = dynamicCSPPolicy,
                EnableSRIValidation = paymentGuardSettings.EnableSRIValidation,
                CurrentPageUrl = currentPage,
                TrustedDomains = _sriHelper.TrustedDomains(paymentGuardSettings),
                PaymentProviders = _sriHelper.TrustedPaymentProviders(paymentGuardSettings),
                LocalLibraryPatterns = _sriHelper.LocalLibraryPatterns()
            };

            return View(model);
        }
    }
}