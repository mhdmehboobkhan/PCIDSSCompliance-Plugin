using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Nop.Core;
using Nop.Core.Domain.Stores;

namespace Nop.Plugin.Misc.PaymentGuard.Helpers
{
    /// <summary>
    /// Helper for generating and managing Subresource Integrity (SRI) hashes
    /// </summary>
    public partial class SRIHelper
    {
        #region Fields

        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public SRIHelper(IWebHostEnvironment webHostEnvironment, 
            IWebHelper webHelper)
        {
            _webHostEnvironment = webHostEnvironment;
            _webHelper = webHelper;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Convert virtual path to physical path
        /// </summary>
        /// <param name="virtualPath">Virtual path starting with ~/</param>
        /// <returns>Physical file path</returns>
        private string GetPhysicalPath(string virtualPath)
        {
            if (virtualPath.StartsWith("~/"))
            {
                virtualPath = virtualPath.Substring(2);
            }

            var rootPath = _webHostEnvironment.WebRootPath;
            if (virtualPath.ToLower().Contains("plugins")) 
            {
                rootPath = _webHostEnvironment.ContentRootPath;
            }

            return Path.Combine(rootPath, virtualPath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Generate hash for content using specified algorithm
        /// </summary>
        /// <param name="content">File content</param>
        /// <param name="algorithm">Hash algorithm</param>
        /// <returns>Base64 encoded hash</returns>
        private string GenerateHash(object content, string algorithm)
        {
            byte[] bytes = content switch
            {
                string str => Encoding.UTF8.GetBytes(str),
                byte[] byteArray => byteArray,
                _ => throw new ArgumentException("Content must be either string or byte array")
            };

            return algorithm.ToLower() switch
            {
                "sha256" => Convert.ToBase64String(SHA256.HashData(bytes)),
                "sha384" => Convert.ToBase64String(SHA384.HashData(bytes)),
                "sha512" => Convert.ToBase64String(SHA512.HashData(bytes)),
                _ => Convert.ToBase64String(SHA384.HashData(bytes)) // Default
            };
        }

        #endregion

        #region Methods

        /// <summary>
        /// Generate SRI hash for a local script file
        /// </summary>
        /// <param name="scriptPath">Relative path to script (e.g., "~/Plugins/Misc.PaymentGuard/scripts/paymentguard-monitor.js")</param>
        /// <param name="algorithm">Hash algorithm (sha256, sha384, sha512)</param>
        /// <returns>SRI hash string</returns>
        public virtual string GenerateSRIHash(string scriptPath, string algorithm = "sha384")
        {
            if (string.IsNullOrEmpty(scriptPath))
                return string.Empty;

            try
            {
                // Convert virtual path to physical path
                var physicalPath = GetPhysicalPath(scriptPath);

                if (!File.Exists(physicalPath))
                    return string.Empty;

                // Read file content
                var fileContent = File.ReadAllText(physicalPath, Encoding.UTF8);

                // Generate hash
                var hash = GenerateHash(fileContent, algorithm);
                var sriHash = $"{algorithm}-{hash}";
                return sriHash;
            }
            catch (Exception)
            {
                // Return empty string on error - SRI is optional
                return string.Empty;
            }
        }

        /// <summary>
        /// Generate SRI hash for external script URL
        /// </summary>
        /// <param name="scriptUrl">External script URL</param>
        /// <param name="algorithm">Hash algorithm</param>
        /// <returns>SRI hash string</returns>
        public virtual async Task<string> GenerateExternalSRIHashAsync(string scriptUrl, string algorithm = "sha384")
        {
            if (string.IsNullOrEmpty(scriptUrl))
                return string.Empty;

            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10); // Set timeout

                var contentBytes = await httpClient.GetByteArrayAsync(scriptUrl);
                //var content = await httpClient.GetStringAsync(scriptUrl);
                var hash = GenerateHash(contentBytes, algorithm);
                var sriHash = $"{algorithm}-{hash}";
                return sriHash;
            }
            catch (Exception)
            {
                // Return empty string on error - SRI is optional
                return string.Empty;
            }
        }

        public virtual List<string> FilterExternalScripts(List<string> allScripts, Store store)
        {
            var externalScripts = new List<string>();
            var storeUrl = store.Url?.TrimEnd('/') ?? "";

            foreach (var scriptUrl in allScripts)
            {
                if (IsLocalScript(scriptUrl, storeUrl))
                    continue;

                externalScripts.Add(scriptUrl);
            }

            return externalScripts;
        }

        public virtual string[] LocalLibraryPatterns()
        {
            var localLibraryPatterns = new[]
            {
                "/lib/",
                "/js/",
                "/scripts/",
                "/assets/",
                "lib_npm",
                "jquery.min.js",
                "bootstrap.min.js",
                "admin.common.js",
                "adminlte.min.js",
                "jquery-ui.min.js",
                "jquery.validate",
                "bootstrap.bundle.min.js",
                "jquery-migrate"
            };
            return localLibraryPatterns;
        }

        public virtual bool IsLocalScript(string scriptUrl, string storeUrl)
        {
            // Check for relative URLs
            if (scriptUrl.StartsWith('/') || scriptUrl.StartsWith("~/"))
                return true;

            // Check for same origin (store URL)
            if (!string.IsNullOrEmpty(storeUrl) && scriptUrl.StartsWith(storeUrl, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check for localhost and local IPs
            if (scriptUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                scriptUrl.Contains("127.0.0.1") ||
                scriptUrl.Contains("::1"))
                return true;

            // Skip PaymentGuard's own scripts
            if (scriptUrl.Contains("PaymentGuard", StringComparison.OrdinalIgnoreCase) ||
                scriptUrl.Contains("paymentguard", StringComparison.OrdinalIgnoreCase))
                return true;

            // Skip common local libraries that are typically bundled
            if (LocalLibraryPatterns().Any(pattern => scriptUrl.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        public virtual string[] TrustedPaymentProviders(PaymentGuardSettings settings)
        {
            var trustedPaymentProviders = settings.PaymentProviders?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim().ToLowerInvariant()).ToArray() ?? Array.Empty<string>();
            return trustedPaymentProviders;
        }

        public virtual string[] TrustedDomains(PaymentGuardSettings settings)
        {
            var trustedDomains = settings.TrustedDomains?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim().ToLowerInvariant()).ToArray() ?? Array.Empty<string>();
            return trustedDomains;
        }

        public virtual bool IsTrustedDomain(PaymentGuardSettings settings, string scriptUrl)
        {
            if (string.IsNullOrEmpty(scriptUrl))
                return false;

            return TrustedDomains(settings).Any(domain => scriptUrl.Contains(domain, StringComparison.OrdinalIgnoreCase));
        }

        #endregion
    }
}