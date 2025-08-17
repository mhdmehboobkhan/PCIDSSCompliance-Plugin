using FluentValidation;
using Nop.Plugin.Misc.PaymentGuard.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Plugin.Misc.PaymentGuard.Validators
{
    public partial class ConfigurationModelValidator : BaseNopValidator<ConfigurationModel>
    {
        public ConfigurationModelValidator(ILocalizationService localizationService)
        {
            // Alert Settings Validation
            RuleFor(x => x.MaxAlertFrequency)
                .InclusiveBetween(1, 168)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Configure.Fields.MaxAlertFrequency.Range"));

            // Maintenance Settings Validation
            RuleFor(x => x.LogRetentionDays)
                .InclusiveBetween(1, 365)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Configure.Fields.LogRetentionDays.Range"));

            RuleFor(x => x.AlertRetentionDays)
                .InclusiveBetween(1, 365)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Configure.Fields.AlertRetentionDays.Range"));

            RuleFor(x => x.CacheExpirationMinutes)
                .InclusiveBetween(1, 1440)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Configure.Fields.CacheExpirationMinutes.Range"));

            // API Settings Validation
            RuleFor(x => x.ApiRateLimitPerHour)
                .InclusiveBetween(1, 100000)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Configure.Fields.ApiRateLimitPerHour.Range"));

            RuleFor(x => x.WhitelistedIPs)
                .Must(BeValidIPList)
                .When(x => !string.IsNullOrEmpty(x.WhitelistedIPs))
                .WithMessage("Please enter valid IP addresses separated by commas (e.g., 192.168.1.1, 10.0.0.1)");

            // CSP Policy Validation
            RuleFor(x => x.CSPPolicy)
                .Must(BeValidCSPPolicy)
                .When(x => !string.IsNullOrEmpty(x.CSPPolicy))
                .WithMessage("Please enter a valid Content Security Policy directive");

            // Monitored Pages Validation
            RuleFor(x => x.MonitoredPages)
                .Must(BeValidPagePaths)
                .When(x => !string.IsNullOrEmpty(x.MonitoredPages))
                .WithMessage("Please enter valid page paths separated by commas (e.g., /checkout, /cart)");

            // Business Logic Validation
            RuleFor(x => x.AlertRetentionDays)
                .LessThanOrEqualTo(x => x.LogRetentionDays)
                .WithMessage("Alert retention period should not exceed log retention period");
        }

        private static bool BeValidIPList(string ipList)
        {
            if (string.IsNullOrWhiteSpace(ipList))
                return true;

            var ips = ipList.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var ip in ips)
            {
                var trimmedIp = ip.Trim();
                if (!IPAddress.TryParse(trimmedIp, out _))
                {
                    // Check for IP ranges (CIDR notation)
                    if (trimmedIp.Contains('/'))
                    {
                        var parts = trimmedIp.Split('/');
                        if (parts.Length != 2 ||
                            !IPAddress.TryParse(parts[0], out _) ||
                            !int.TryParse(parts[1], out var cidr) ||
                            cidr < 0 || cidr > 32)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool BeValidCSPPolicy(string cspPolicy)
        {
            if (string.IsNullOrWhiteSpace(cspPolicy))
                return true;

            // Basic CSP validation - check for common directives
            var validDirectives = new[]
            {
                "script-src", "style-src", "img-src", "font-src", "connect-src",
                "media-src", "object-src", "frame-src", "child-src", "worker-src",
                "frame-ancestors", "form-action", "base-uri", "manifest-src",
                "default-src", "upgrade-insecure-requests", "block-all-mixed-content"
            };

            // Very basic validation - just check if it contains at least one valid directive
            return validDirectives.Any(directive => cspPolicy.Contains(directive, StringComparison.OrdinalIgnoreCase));
        }

        private static bool BeValidPagePaths(string pagePaths)
        {
            if (string.IsNullOrWhiteSpace(pagePaths))
                return true;

            var paths = pagePaths.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var path in paths)
            {
                var trimmedPath = path.Trim();

                // Check if path starts with / and contains only valid URL characters
                if (!trimmedPath.StartsWith('/') ||
                    !Regex.IsMatch(trimmedPath, @"^/[a-zA-Z0-9\-_/]*$"))
                {
                    return false;
                }
            }

            return true;
        }
    }
}