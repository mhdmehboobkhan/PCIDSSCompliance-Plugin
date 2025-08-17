using System.Text.Json;
using HtmlAgilityPack;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Dto;
using Nop.Plugin.Misc.PaymentGuard.Helpers;
using Nop.Services.Logging;
using Nop.Services.Stores;
using Nop.Web.Framework.UI.Paging;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial class MonitoringService : IMonitoringService
    {
        #region Fields

        private readonly IRepository<ScriptMonitoringLog> _monitoringLogRepository;
        private readonly IAuthorizedScriptService _authorizedScriptService;
        private readonly HttpClient _httpClient;
        private readonly ISRIValidationService _sriValidationService;
        private readonly ILogger _logger;
        private readonly IComplianceAlertService _complianceAlertService;
        private readonly IStoreService _storeService;
        private readonly IEmailAlertService _emailAlertService;
        private readonly SRIHelper _sriHelper;
        
        #endregion

        #region Ctor

        public MonitoringService(IRepository<ScriptMonitoringLog> monitoringLogRepository,
            IAuthorizedScriptService authorizedScriptService,
            HttpClient httpClient,
            ISRIValidationService sriValidationService,
            ILogger logger,
            IComplianceAlertService complianceAlertService,
            IStoreService storeService,
            IEmailAlertService emailAlertService,
            SRIHelper sriHelper)
        {
            _monitoringLogRepository = monitoringLogRepository;
            _authorizedScriptService = authorizedScriptService;
            _httpClient = httpClient;
            _sriValidationService = sriValidationService;
            _logger = logger;
            _complianceAlertService = complianceAlertService;
            _storeService = storeService;
            _emailAlertService = emailAlertService;
            _sriHelper = sriHelper;
        }

        #endregion

        #region Utilities

        private ScriptValidationResult CreateUnauthorizedResult(string scriptUrl)
        {
            return new ScriptValidationResult
            {
                ScriptUrl = scriptUrl,
                IsAuthorized = false,
                HasValidSRI = false,
                SRIValidation = new SRIValidationResult
                {
                    IsValid = false,
                    Error = "Script is not in authorized list",
                    ScriptUrl = scriptUrl
                }
            };
        }

        private async Task<ScriptValidationResult> ValidateWithBrowserIntegrity(AuthorizedScript authorizedScript,
            PaymentGuardSettings settings, int storeId, string browserIntegrity, string pageUrl)
        {
            var result = new ScriptValidationResult { ScriptUrl = authorizedScript.ScriptUrl, IsAuthorized = true };

            if (string.IsNullOrEmpty(authorizedScript.ScriptHash))
            {
                // No stored hash - handle missing hash scenario
                return await HandleMissingStoredHash(settings, authorizedScript, browserIntegrity, pageUrl);
            }

            // Compare browser hash with stored ScriptHash
            var hashMatch = string.Equals(browserIntegrity, authorizedScript.ScriptHash, StringComparison.OrdinalIgnoreCase);

            if (hashMatch)
            {
                // Hashes match - validation successful
                result.HasValidSRI = true;
                result.SRIValidation = new SRIValidationResult
                {
                    IsValid = true,
                    CurrentHash = browserIntegrity,
                    ExpectedHash = authorizedScript.ScriptHash,
                    ScriptUrl = authorizedScript.ScriptUrl
                };

                // Update LastVerifiedUtc
                authorizedScript.LastVerifiedUtc = DateTime.UtcNow;
                await _authorizedScriptService.UpdateAuthorizedScriptAsync(authorizedScript);

                await _logger.InformationAsync($"SRI validation passed: {authorizedScript.ScriptUrl}");
            }
            else
            {
                // Hash mismatch - potential security issue
                return await HandleHashMismatch(authorizedScript, storeId, browserIntegrity, settings, pageUrl);
            }

            return result;
        }

        private async Task<ScriptValidationResult> ValidateWithoutBrowserIntegrity(AuthorizedScript authorizedScript, 
            PaymentGuardSettings settings, string pageUrl)
        {
            var result = new ScriptValidationResult { ScriptUrl = authorizedScript.ScriptUrl, IsAuthorized = true };

            result.HasValidSRI = false;
            result.SRIValidation = new SRIValidationResult
            {
                IsValid = false,
                Error = "No integrity attribute present in browser",
                ScriptUrl = authorizedScript.ScriptUrl,
                ExpectedHash = authorizedScript.ScriptHash
            };

            // If we have a stored hash but browser doesn't provide integrity, this is a security concern
            if (!string.IsNullOrEmpty(authorizedScript.ScriptHash))
            {
                result.SRIValidation.Error = "Script should have integrity attribute but browser provided none";
                await _logger.WarningAsync($"Missing SRI in browser for script that should have it: {authorizedScript.ScriptUrl}");

                // Create alert for missing SRI
                await CreateMissingSRIAlert(authorizedScript, pageUrl);
            }

            return result;
        }

        private async Task<ScriptValidationResult> HandleMissingStoredHash(PaymentGuardSettings settings, AuthorizedScript authorizedScript, 
            string browserIntegrity, string pageUrl)
        {
            var result = new ScriptValidationResult { ScriptUrl = authorizedScript.ScriptUrl, IsAuthorized = true };

            // Script is authorized but has no stored hash
            // Option 1: Auto-update with browser hash (if trusted domain)
            // Option 2: Require manual hash update

            if (_sriHelper.IsTrustedDomain(settings, authorizedScript.ScriptUrl))
            {
                // Auto-update hash for trusted domains
                authorizedScript.ScriptHash = browserIntegrity;
                authorizedScript.LastVerifiedUtc = DateTime.UtcNow;
                await _authorizedScriptService.UpdateAuthorizedScriptAsync(authorizedScript);

                result.HasValidSRI = true;
                result.SRIValidation = new SRIValidationResult
                {
                    IsValid = true,
                    CurrentHash = browserIntegrity,
                    ExpectedHash = browserIntegrity,
                    ScriptUrl = authorizedScript.ScriptUrl,
                    Error = "Hash auto-updated for trusted domain"
                };

                await _logger.InformationAsync($"Auto-updated hash for trusted script: {authorizedScript.ScriptUrl}");
                await CreateHashUpdatedAlert(authorizedScript, null, browserIntegrity, pageUrl);
            }
            else
            {
                // Require manual update for non-trusted domains
                result.HasValidSRI = false;
                result.SRIValidation = new SRIValidationResult
                {
                    IsValid = false,
                    CurrentHash = browserIntegrity,
                    ExpectedHash = null,
                    ScriptUrl = authorizedScript.ScriptUrl,
                    Error = "Authorized script has no stored hash - manual update required"
                };

                await CreateHashMissingAlert(authorizedScript, browserIntegrity, pageUrl);
            }

            return result;
        }

        private async Task<ScriptValidationResult> HandleHashMismatch(AuthorizedScript authorizedScript, int storeId, 
            string browserIntegrity, PaymentGuardSettings settings, string pageUrl)
        {
            var result = new ScriptValidationResult { ScriptUrl = authorizedScript.ScriptUrl, IsAuthorized = true };

            await _logger.WarningAsync($"SRI Hash Mismatch - Script: {authorizedScript.ScriptUrl}, Stored: {authorizedScript.ScriptHash}, Browser: {browserIntegrity}");

            // Check if script content actually changed by getting current content hash
            var currentContentValidation = await _sriValidationService.ValidateScriptIntegrityAsync(authorizedScript.ScriptUrl, null);
            var contentChanged = !string.IsNullOrEmpty(currentContentValidation.CurrentHash) &&
                                !string.Equals(currentContentValidation.CurrentHash, authorizedScript.ScriptHash, StringComparison.OrdinalIgnoreCase);

            if (contentChanged)
            {
                // Script content has actually changed - serious security concern
                await _logger.ErrorAsync($"SECURITY ALERT - Script content changed: {authorizedScript.ScriptUrl}");
                await CreateScriptContentChangedAlert(settings, 
                    authorizedScript, storeId, browserIntegrity, 
                    currentContentValidation.CurrentHash, pageUrl);

                result.HasValidSRI = false;
                result.SRIValidation = new SRIValidationResult
                {
                    IsValid = false,
                    CurrentHash = browserIntegrity,
                    ExpectedHash = authorizedScript.ScriptHash,
                    ScriptUrl = authorizedScript.ScriptUrl,
                    Error = $"Script content changed - needs re-authorization. Current content hash: {currentContentValidation.CurrentHash}"
                };
            }
            else
            {
                // Browser hash differs but content is same - could be encoding issue or suspicious
                result.HasValidSRI = false;
                result.SRIValidation = new SRIValidationResult
                {
                    IsValid = false,
                    CurrentHash = browserIntegrity,
                    ExpectedHash = authorizedScript.ScriptHash,
                    ScriptUrl = authorizedScript.ScriptUrl,
                    Error = "Browser integrity hash differs from stored hash but content appears unchanged"
                };

                await _logger.WarningAsync($"Suspicious hash mismatch: {authorizedScript.ScriptUrl}");
            }
            return result;
        }

        private async Task CreateMissingSRIAlert(AuthorizedScript authorizedScript, string pageUrl)
        {
            await _complianceAlertService.CreateIntegrityFailureAlertAsync(
                authorizedScript.StoreId,
                authorizedScript.ScriptUrl,
                pageUrl,
                JsonSerializer.Serialize(new
                {
                    AlertType = "missing-sri",
                    ScriptId = authorizedScript.Id,
                    ExpectedHash = authorizedScript.ScriptHash,
                    Issue = "Script should have integrity attribute but browser provided none",
                    Timestamp = DateTime.UtcNow
                })
            );
        }

        private async Task CreateHashMissingAlert(AuthorizedScript authorizedScript, string browserIntegrity, string pageUrl)
        {
            await _complianceAlertService.CreateIntegrityFailureAlertAsync(
                authorizedScript.StoreId,
                authorizedScript.ScriptUrl,
                pageUrl,
                JsonSerializer.Serialize(new
                {
                    AlertType = "hash-missing-in-authorized-script",
                    ScriptId = authorizedScript.Id,
                    BrowserHash = browserIntegrity,
                    Issue = "Authorized script has no stored hash",
                    Recommendation = "Update authorized script with proper hash",
                    Timestamp = DateTime.UtcNow
                })
            );
        }

        private async Task CreateHashUpdatedAlert(AuthorizedScript authorizedScript, string oldHash, string newHash, string pageUrl)
        {
            await _complianceAlertService.CreateIntegrityFailureAlertAsync(
                authorizedScript.StoreId,
                authorizedScript.ScriptUrl,
                pageUrl,
                JsonSerializer.Serialize(new
                {
                    AlertType = "hash-auto-updated",
                    ScriptId = authorizedScript.Id,
                    OldHash = oldHash,
                    NewHash = newHash,
                    Issue = "Script hash automatically updated",
                    Timestamp = DateTime.UtcNow
                })
            );
        }

        private async Task CreateScriptContentChangedAlert(PaymentGuardSettings settings, 
            AuthorizedScript authorizedScript, int storeId, string browserHash, string currentContentHash, string pageUrl)
        {
            // Create critical alert for script content change
            await _complianceAlertService.CreateIntegrityFailureAlertAsync(
                authorizedScript.StoreId,
                authorizedScript.ScriptUrl,
                pageUrl,
                JsonSerializer.Serialize(new
                {
                    AlertType = "script-content-changed",
                    AlertLevel = "critical",
                    ScriptId = authorizedScript.Id,
                    StoredHash = authorizedScript.ScriptHash,
                    BrowserHash = browserHash,
                    CurrentContentHash = currentContentHash,
                    Issue = "Script content has changed and needs re-authorization",
                    SecurityImplication = "Potential security compromise - script content modified",
                    RecommendedAction = "Immediately review script content and re-authorize if legitimate",
                    Timestamp = DateTime.UtcNow
                })
            );

            // Also send email alert if enabled
            if (settings.EnableEmailAlerts)
            {
                var store = await _storeService.GetStoreByIdAsync(storeId);
                await _emailAlertService.SendScriptChangeAlertAsync(
                    authorizedScript.ScriptUrl,
                    store.Id
                );
            }
        }

        #endregion

        #region Methods

        /*public virtual async Task<ScriptMonitoringLog> PerformMonitoringCheckAsync(string pageUrl, int storeId)
        {
            var detectedScripts = await ExtractScriptsFromPageAsync(pageUrl);
            var securityHeaders = await ExtractSecurityHeadersAsync(pageUrl);

            var unauthorizedScripts = new List<string>();
            var authorizedCount = 0;
            var integrityFailures = new List<string>();

            foreach (var scriptUrl in detectedScripts)
            {
                var isAuthorized = await _authorizedScriptService.IsScriptAuthorizedAsync(scriptUrl, storeId);
                if (isAuthorized)
                {
                    authorizedCount++;

                    // NEW: Validate script integrity for authorized scripts
                    var hasValidIntegrity = await _authorizedScriptService.ValidateScriptIntegrityAsync(scriptUrl, null);
                    if (!hasValidIntegrity)
                    {
                        integrityFailures.Add(scriptUrl);
                        // Consider this as a security issue
                        await _logger.WarningAsync($"Script integrity validation failed for {scriptUrl}");
                    }
                }
                else
                    unauthorizedScripts.Add(scriptUrl);
            }

            var log = new ScriptMonitoringLog
            {
                StoreId = storeId,
                PageUrl = pageUrl,
                DetectedScripts = JsonSerializer.Serialize(detectedScripts),
                HttpHeaders = JsonSerializer.Serialize(securityHeaders),
                HasUnauthorizedScripts = unauthorizedScripts.Any(),
                UnauthorizedScripts = JsonSerializer.Serialize(unauthorizedScripts),
                CheckedOnUtc = DateTime.UtcNow,
                CheckType = "scheduled",
                TotalScriptsFound = detectedScripts.Count,
                AuthorizedScriptsCount = authorizedCount,
                UnauthorizedScriptsCount = unauthorizedScripts.Count
            };

            await InsertMonitoringLogAsync(log);
            return log;
        }*/

        public virtual async Task<IList<string>> ExtractScriptsFromPageAsync(string pageUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(pageUrl);
                response.EnsureSuccessStatusCode();

                var htmlContent = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                var scriptUrls = new List<string>();

                // Extract external scripts
                var scriptNodes = doc.DocumentNode.SelectNodes("//script[@src]");
                if (scriptNodes != null)
                {
                    foreach (var node in scriptNodes)
                    {
                        var src = node.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(src))
                        {
                            // Convert relative URLs to absolute
                            if (src.StartsWith("//"))
                                src = "https:" + src;
                            else if (src.StartsWith("/"))
                                src = new Uri(new Uri(pageUrl), src).ToString();
                            else if (!src.StartsWith("http"))
                                src = new Uri(new Uri(pageUrl), src).ToString();

                            scriptUrls.Add(src);
                        }
                    }
                }

                // Extract inline scripts (for monitoring purposes)
                var inlineScripts = doc.DocumentNode.SelectNodes("//script[not(@src)]");
                if (inlineScripts != null)
                {
                    foreach (var node in inlineScripts)
                    {
                        var content = node.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(content))
                        {
                            // Create a hash-based identifier for inline scripts
                            var hash = Convert.ToBase64String(
                                System.Security.Cryptography.SHA256.HashData(
                                    System.Text.Encoding.UTF8.GetBytes(content)))[..16];
                            scriptUrls.Add($"inline-script-{hash}");
                        }
                    }
                }

                return scriptUrls.Distinct().ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public virtual async Task<IDictionary<string, string>> ExtractSecurityHeadersAsync(string pageUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(pageUrl);
                var securityHeaders = new Dictionary<string, string>();

                // Check for security-related headers
                var headersToCheck = new[]
                {
                    "Content-Security-Policy",
                    "X-Content-Type-Options",
                    "X-Frame-Options",
                    "X-XSS-Protection",
                    "Strict-Transport-Security",
                    "Referrer-Policy"
                };

                foreach (var headerName in headersToCheck)
                {
                    if (response.Headers.TryGetValues(headerName, out var values))
                    {
                        securityHeaders[headerName] = string.Join(", ", values);
                    }
                    else if (response.Content.Headers.TryGetValues(headerName, out values))
                    {
                        securityHeaders[headerName] = string.Join(", ", values);
                    }
                    else
                    {
                        securityHeaders[headerName] = ""; // Missing header
                    }
                }

                return securityHeaders;
            }
            catch (Exception)
            {
                return new Dictionary<string, string>();
            }
        }

        public virtual async Task<IPagedList<ScriptMonitoringLog>> GetMonitoringLogsAsync(int storeId = 0,
            DateTime? fromDate = null, DateTime? toDate = null, bool? hasUnauthorizedScripts = null, 
            int pageIndex = 0, int pageSize = int.MaxValue)
        {
            var query = _monitoringLogRepository.Table;

            if (storeId > 0)
                query = query.Where(log => log.StoreId == storeId);

            if (hasUnauthorizedScripts.HasValue)
                query = query.Where(log => log.HasUnauthorizedScripts == hasUnauthorizedScripts.Value);

            if (fromDate.HasValue)
                query = query.Where(log => log.CheckedOnUtc >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(log => log.CheckedOnUtc <= toDate.Value);

            query = query.OrderByDescending(log => log.CheckedOnUtc);

            return await query.ToPagedListAsync(pageIndex, pageSize);
        }

        public virtual async Task<ScriptMonitoringLog> GetMonitoringLogByIdAsync(int logId)
        {
            return await _monitoringLogRepository.GetByIdAsync(logId);
        }

        public virtual async Task InsertMonitoringLogAsync(ScriptMonitoringLog log)
        {
            ArgumentNullException.ThrowIfNull(log);
            await _monitoringLogRepository.InsertAsync(log);
        }

        public virtual async Task<ComplianceReport> GenerateComplianceReportAsync(int storeId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _monitoringLogRepository.Table;

            if (storeId > 0)
                query = query.Where(log => log.StoreId == storeId);

            if (fromDate.HasValue)
                query = query.Where(log => log.CheckedOnUtc >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(log => log.CheckedOnUtc <= toDate.Value);

            var logs = await query.ToListAsync();

            var report = new ComplianceReport
            {
                TotalChecksPerformed = logs.Count,
                AlertsGenerated = logs.Count(l => l.HasUnauthorizedScripts),
                LastCheckDate = logs.Any() ? logs.Max(l => l.CheckedOnUtc) : DateTime.MinValue
            };

            if (logs.Any())
            {
                report.TotalScriptsMonitored = logs.Sum(l => l.TotalScriptsFound);
                report.AuthorizedScriptsCount = logs.Sum(l => l.AuthorizedScriptsCount);
                report.UnauthorizedScriptsCount = logs.Sum(l => l.UnauthorizedScriptsCount);

                // Calculate compliance score (percentage of authorized scripts)
                var totalScripts = report.TotalScriptsMonitored;
                report.ComplianceScore = totalScripts > 0
                    ? (double)report.AuthorizedScriptsCount / totalScripts * 100
                    : 100;

                // Get most common unauthorized scripts
                var unauthorizedScripts = new List<string>();
                foreach (var log in logs.Where(l => l.HasUnauthorizedScripts))
                {
                    try
                    {
                        var scripts = JsonSerializer.Deserialize<List<string>>(log.UnauthorizedScripts ?? "[]");
                        unauthorizedScripts.AddRange(scripts);
                    }
                    catch { }
                }

                report.MostCommonUnauthorizedScripts = unauthorizedScripts
                    .GroupBy(s => s)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => $"{g.Key} ({g.Count()} times)")
                    .ToList();
            }

            return report;
        }

        public virtual async Task<ScriptValidationResult> ValidateScriptWithSRIAsync(PaymentGuardSettings guardSettings, 
            int storeId, string pageUrl, string scriptUrl, string browserIntegrity = null)
        {
            // 1. Get authorized script from existing table
            var (isAuthorized, authorizedScript) = await _authorizedScriptService.IsScriptAuthorizedAsync(scriptUrl, storeId);
            if (!isAuthorized)
            {
                return CreateUnauthorizedResult(scriptUrl);
            }

            // 2. If browser provided integrity hash, validate against stored ScriptHash
            if (!string.IsNullOrEmpty(browserIntegrity))
            {
                return await ValidateWithBrowserIntegrity(authorizedScript, guardSettings, storeId, browserIntegrity, pageUrl);
            }
            else
            {
                return await ValidateWithoutBrowserIntegrity(authorizedScript, guardSettings, pageUrl);
            }
        }

        #endregion
    }
}