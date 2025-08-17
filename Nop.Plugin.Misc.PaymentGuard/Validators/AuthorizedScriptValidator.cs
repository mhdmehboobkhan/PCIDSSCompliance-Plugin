using FluentValidation;
using Nop.Core.Domain.Logging;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Models;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Misc.PaymentGuard.Validators
{
    public partial class AuthorizedScriptValidator : BaseNopValidator<AuthorizedScriptModel>
    {
        public AuthorizedScriptValidator(ILocalizationService localizationService)
        {
            RuleFor(x => x.ScriptUrl).NotEmpty()
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptUrl.Required"))
                .Must(BeAValidUrl)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptUrl.InvalidFormat"))
                .Must(BeHttpsUrl)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptUrl.MustBeHttps"));
            RuleFor(x => x.Purpose).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Purpose.Required"));
            RuleFor(x => x.Justification).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Justification.Required"));
        }

        private static bool BeAValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return true; // Let NotEmpty handle null/empty validation

            return Uri.TryCreate(url, UriKind.Absolute, out Uri result)
                   && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }

        private static bool BeHttpsUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return true; // Let NotEmpty handle null/empty validation

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri result))
                return false;

            // Allow localhost with HTTP or HTTPS
            if (IsLocalhost(result))
                return result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps;

            // For all other domains, require HTTPS
            return result.Scheme == Uri.UriSchemeHttps;
        }

        private static bool IsLocalhost(Uri uri)
        {
            return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.StartsWith("192.168.", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.StartsWith("10.", StringComparison.OrdinalIgnoreCase) ||
                   (uri.Host.StartsWith("172.", StringComparison.OrdinalIgnoreCase) &&
                    IsPrivateClass172(uri.Host));
        }

        private static bool IsPrivateClass172(string host)
        {
            var parts = host.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int secondOctet))
            {
                return secondOctet >= 16 && secondOctet <= 31;
            }
            return false;
        }
    }
}