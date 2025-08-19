using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Dto;
using Nop.Plugin.Misc.PaymentGuard.Helpers;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Misc.PaymentGuard.Controllers
{
    public class PaymentGuardApiController : BasePluginController
    {
        #region Fields

        private readonly IAuthorizedScriptService _authorizedScriptService;
        private readonly IMonitoringService _monitoringService;
        private readonly IComplianceAlertService _complianceAlertService;
        private readonly IEmailAlertService _emailAlertService;
        private readonly IStoreContext _storeContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILogger _logger;
        private readonly SRIHelper _sriHelper;
        private readonly ISRIValidationService _sriValidationService;
        private readonly PaymentGuardSettings _paymentGuardSettings;

        #endregion

        #region Ctor

        public PaymentGuardApiController(IAuthorizedScriptService authorizedScriptService,
            IMonitoringService monitoringService,
            IComplianceAlertService complianceAlertService,
            IEmailAlertService emailAlertService,
            IStoreContext storeContext,
            IStoreService storeService,
            ISettingService settingService,
            ILogger logger,
            SRIHelper sriHelper,
            ISRIValidationService sriValidationService,
            PaymentGuardSettings paymentGuardSettings)
        {
            _authorizedScriptService = authorizedScriptService;
            _monitoringService = monitoringService;
            _complianceAlertService = complianceAlertService;
            _emailAlertService = emailAlertService;
            _storeContext = storeContext;
            _storeService = storeService;
            _settingService = settingService;
            _logger = logger;
            _sriHelper = sriHelper;
            _sriValidationService = sriValidationService;
            _paymentGuardSettings = paymentGuardSettings;
        }

        #endregion

        #region Methods

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ValidateScript")]
        public async Task<IActionResult> ValidateScript([FromBody] ValidateScriptRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                var (isAuthorized, _) = await _authorizedScriptService.IsScriptAuthorizedAsync(request.ScriptUrl, store.Id);

                return Json(new { isAuthorized = isAuthorized });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error validating script {request.ScriptUrl}", ex);
                return Json(new { isAuthorized = false, error = "Validation failed" });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ReportScripts")]
        public async Task<IActionResult> ReportScripts([FromBody] ReportScriptsRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();

                //await _logger.InformationAsync($"Scripts reported from {request.PageUrl}: {request.Scripts.Count} scripts");

                // Process the reported scripts
                var unauthorizedScripts = new List<string>();

                foreach (var scriptUrl in request.Scripts)
                {
                    var (isAuthorized, _) = await _authorizedScriptService.IsScriptAuthorizedAsync(scriptUrl, store.Id);
                    if (!isAuthorized)
                    {
                        unauthorizedScripts.Add(scriptUrl);
                    }
                }

                if (unauthorizedScripts.Any())
                {
                    await _logger.InsertLogAsync(LogLevel.Error, "Unauthorized scripts detected from client-side",
                        string.Join(", ", unauthorizedScripts));
                }

                return Json(new
                {
                    success = true,
                    unauthorizedCount = unauthorizedScripts.Count,
                    unauthorizedScripts = unauthorizedScripts
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error processing reported scripts from {request.PageUrl}", ex);
                return Json(new { success = false, error = "Processing failed" });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ReportBlockedScript")]
        public async Task<IActionResult> ReportBlockedScript([FromBody] BlockedScriptDto request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();

                // Create high-priority security alert
                await _complianceAlertService.CreateSecurityAlertAsync(
                    store.Id,
                    request.ScriptUrl,
                    request.PageUrl,
                    JsonSerializer.Serialize(new
                    {
                        ScriptUrl = request.ScriptUrl,
                        PageUrl = request.PageUrl,
                        BlockReason = request.BlockReason,
                        UserAgent = request.UserAgent,
                        DetectedAt = DateTime.UtcNow,
                    })
                );

                // Send immediate email alert
                if (_paymentGuardSettings.EnableEmailAlerts)
                {
                    await _emailAlertService.SendBlockedScriptAlertAsync(
                        request.ScriptUrl,
                        request.PageUrl,
                        store.Id
                    );
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error reporting blocked script", ex);
                return Json(new { success = false });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ReportViolation")]
        public async Task<IActionResult> ReportViolation([FromBody] ReportViolationRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                //await _logger.WarningAsync($"Security violation reported: {request.ViolationType} - {request.ScriptUrl} on {request.PageUrl}");

                // Create compliance alert based on violation type
                ComplianceAlert alert = null;

                switch (request.ViolationType?.ToLower())
                {
                    case "unauthorized-script":
                        alert = await _complianceAlertService.CreateUnauthorizedScriptAlertAsync(
                            store.Id,
                            request.ScriptUrl,
                            request.PageUrl,
                            JsonSerializer.Serialize(new
                            {
                                ViolationType = request.ViolationType,
                                Timestamp = request.Timestamp,
                                UserAgent = request.UserAgent,
                                Source = "client-side-monitoring"
                            }));
                        break;

                    case "missing-sri-hash":
                        alert = await _complianceAlertService.CreateIntegrityFailureAlertAsync(
                            store.Id,
                            request.ScriptUrl,
                            request.PageUrl,
                            JsonSerializer.Serialize(new
                            {
                                ViolationType = request.ViolationType,
                                Issue = "Script loaded without SRI hash",
                                Timestamp = request.Timestamp,
                                UserAgent = request.UserAgent
                            }));
                        break;

                    case "invalid-sri-format":
                        alert = await _complianceAlertService.CreateIntegrityFailureAlertAsync(
                            store.Id,
                            request.ScriptUrl,
                            request.PageUrl,
                            JsonSerializer.Serialize(new
                            {
                                ViolationType = request.ViolationType,
                                Issue = "Invalid SRI hash format",
                                Timestamp = request.Timestamp,
                                UserAgent = request.UserAgent
                            }));
                        break;

                    default:
                        // Generic security violation
                        alert = await _complianceAlertService.CreateUnauthorizedScriptAlertAsync(
                            store.Id,
                            request.ScriptUrl,
                            request.PageUrl,
                            JsonSerializer.Serialize(request));
                        break;
                }

                // Send email alert if enabled and alert was created (not duplicate)
                if (alert != null && _paymentGuardSettings.EnableEmailAlerts)
                {
                    // Check alert frequency to avoid spam
                    var shouldSendEmail = true;
                    if (_paymentGuardSettings.MaxAlertFrequency > 0)
                    {
                        var recentAlerts = await _complianceAlertService.GetRecentAlertsAsync(store.Id, 
                            _paymentGuardSettings.MaxAlertFrequency, violationType: request.ViolationType, scriptUrl: request.ScriptUrl);
                        
                        var similarRecentAlert = recentAlerts.FirstOrDefault();
                        if (similarRecentAlert != null && similarRecentAlert.EmailSent)
                            shouldSendEmail = false;
                    }

                    if (shouldSendEmail)
                    {
                        try
                        {
                            if (request.ViolationType?.ToLower().Contains("unauthorized") == true)
                            {
                                await _emailAlertService.SendUnauthorizedScriptAlertAsync(
                                    new Domain.ScriptMonitoringLog
                                    {
                                        PageUrl = request.PageUrl,
                                        HasUnauthorizedScripts = true,
                                        UnauthorizedScripts = JsonSerializer.Serialize(new[] { request.ScriptUrl }),
                                        CheckedOnUtc = DateTime.UtcNow,
                                        TotalScriptsFound = 1,
                                        UnauthorizedScriptsCount = 1,
                                        AuthorizedScriptsCount = 0
                                    },
                                    store.Id);
                            }
                            else
                            {
                                await _emailAlertService.SendScriptChangeAlertAsync(
                                    request.ScriptUrl,
                                    store.Id);
                            }

                            // Mark alert as email sent
                            if (alert != null)
                            {
                                alert.EmailSent = true;
                                alert.EmailSentOnUtc = DateTime.UtcNow;
                                await _complianceAlertService.UpdateComplianceAlertAsync(alert);
                            }
                        }
                        catch (Exception emailEx)
                        {
                            await _logger.ErrorAsync($"Failed to send violation alert email", emailEx);
                        }
                    }
                }

                return Json(new
                {
                    success = true,
                    alertId = alert?.Id,
                    message = "Violation reported and processed successfully"
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error processing violation report: {request.ViolationType}", ex);
                return Json(new
                {
                    success = false,
                    error = "Failed to process violation report"
                });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ReportCSPViolation")]
        public async Task<IActionResult> ReportCSPViolation([FromBody] ReportCSPViolationRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();

                var violationDetails = JsonSerializer.Serialize(new
                {
                    BlockedURI = request.Violation.BlockedURI,
                    ViolatedDirective = request.Violation.ViolatedDirective,
                    EffectiveDirective = request.Violation.EffectiveDirective,
                    OriginalPolicy = request.Violation.OriginalPolicy,
                    SourceFile = request.Violation.SourceFile,
                    LineNumber = request.Violation.LineNumber,
                    ColumnNumber = request.Violation.ColumnNumber,
                    Timestamp = request.Timestamp,
                    UserAgent = request.UserAgent,
                    PageUrl = request.PageUrl
                }, new JsonSerializerOptions { WriteIndented = true });

                await _logger.WarningAsync($"CSP violation reported on {request.PageUrl}: {request.Violation.BlockedURI} violated {request.Violation.ViolatedDirective}");

                // Create compliance alert for CSP violation
                var alert = await _complianceAlertService.CreateCSPViolationAlertAsync(
                    store.Id,
                    request.PageUrl,
                    violationDetails);

                // Send email alert if enabled and alert was created (not duplicate)
                if (alert != null && _paymentGuardSettings.EnableEmailAlerts)
                {
                    try
                    {
                        await _emailAlertService.SendCSPViolationAlertAsync(
                            violationDetails,
                            store.Id);

                        // Mark alert as email sent
                        alert.EmailSent = true;
                        alert.EmailSentOnUtc = DateTime.UtcNow;
                        await _complianceAlertService.UpdateComplianceAlertAsync(alert);
                    }
                    catch (Exception emailEx)
                    {
                        await _logger.ErrorAsync($"Failed to send CSP violation alert email", emailEx);
                    }
                }

                return Json(new
                {
                    success = true,
                    alertId = alert?.Id,
                    message = "CSP violation reported and processed successfully"
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error processing CSP violation report", ex);
                return Json(new
                {
                    success = false,
                    error = "Failed to process CSP violation report"
                });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ValidateScriptWithSRI")]
        public async Task<IActionResult> ValidateScriptWithSRI([FromBody] ValidateScriptWithSRIRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();

                // Check authorization first
                var (isAuthorized, _) = await _authorizedScriptService.IsScriptAuthorizedAsync(request.ScriptUrl, store.Id);

                // If script has integrity attribute, validate it
                if (!string.IsNullOrEmpty(request.Integrity))
                {
                    var result = await _monitoringService.ValidateScriptWithSRIAsync(_paymentGuardSettings,
                        store.Id, request.PageUrl, request.ScriptUrl, request.Integrity);

                    //await _logger.InformationAsync($"SRI validation with integrity - Script: {request.ScriptUrl}, Valid: {result.HasValidSRI}, Authorized: {result.IsAuthorized}");

                    return Json(new
                    {
                        success = true,
                        isAuthorized = result.IsAuthorized,
                        hasValidSRI = result.HasValidSRI,
                        sriError = result.SRIValidation?.Error,
                        providedIntegrity = request.Integrity
                    });
                }
                // If forced validation (no integrity provided), generate hash for reference
                else if (request.ForceValidation)
                {
                    try
                    {
                        var generatedHash = await _sriValidationService.ValidateScriptIntegrityAsync(request.ScriptUrl, null);

                        //await _logger.InformationAsync($"Forced SRI validation - Script: {request.ScriptUrl}, Generated hash: {generatedHash.CurrentHash}, Authorized: {isAuthorized}");

                        return Json(new
                        {
                            success = true,
                            isAuthorized = isAuthorized,
                            hasValidSRI = false, // No integrity to validate against
                            generatedHash = generatedHash.CurrentHash,
                            sriError = "No integrity attribute provided",
                            message = "Hash generated for future SRI implementation"
                        });
                    }
                    catch (Exception ex)
                    {
                        await _logger.ErrorAsync($"Error generating hash for script {request.ScriptUrl}", ex);

                        return Json(new
                        {
                            success = true,
                            isAuthorized = isAuthorized,
                            hasValidSRI = false,
                            generatedHash = "",
                            sriError = $"Could not generate hash: {ex.Message}"
                        });
                    }
                }
                // Regular validation without SRI
                else
                {
                    await _logger.InformationAsync($"Regular validation (no SRI) - Script: {request.ScriptUrl}, Authorized: {isAuthorized}");

                    return Json(new
                    {
                        success = true,
                        isAuthorized = isAuthorized,
                        hasValidSRI = false,
                        sriError = "No integrity attribute present"
                    });
                }
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error validating script with SRI {request.ScriptUrl}", ex);
                return Json(new { success = false, error = "Validation failed" });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ReportMonitoringSession")]
        public async Task<IActionResult> ReportMonitoringSession([FromBody] MonitoringSessionRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();

                // Filter and validate reported scripts
                var externalScripts = _sriHelper.FilterExternalScripts(request.DetectedScripts, store);
                var authorizedCount = 0;
                var unauthorizedScripts = new List<string>();

                foreach (var scriptUrl in externalScripts)
                {
                    var (isAuthorized, _) = await _authorizedScriptService.IsScriptAuthorizedAsync(scriptUrl, store.Id);
                    if (isAuthorized)
                        authorizedCount++;
                    else
                        unauthorizedScripts.Add(scriptUrl);
                }

                // Count local scripts as authorized (they don't need explicit authorization)
                var localScriptsCount = request.DetectedScripts.Count - externalScripts.Count;
                authorizedCount += localScriptsCount;

                // Create ScriptMonitoringLog entry
                var log = new ScriptMonitoringLog
                {
                    StoreId = store.Id,
                    PageUrl = request.PageUrl,
                    DetectedScripts = JsonSerializer.Serialize(request.DetectedScripts),
                    HttpHeaders = JsonSerializer.Serialize(request.Headers ?? new Dictionary<string, string>()),
                    HasUnauthorizedScripts = unauthorizedScripts.Any(),
                    UnauthorizedScripts = JsonSerializer.Serialize(unauthorizedScripts),
                    CheckedOnUtc = DateTime.UtcNow,
                    CheckType = request.CheckType ?? "client-side",
                    UserAgent = request.UserAgent,
                    TotalScriptsFound = request.DetectedScripts.Count,
                    AuthorizedScriptsCount = authorizedCount,
                    UnauthorizedScriptsCount = unauthorizedScripts.Count,
                    AlertSent = false
                };

                await _monitoringService.InsertMonitoringLogAsync(log);

                // Create compliance alerts for unauthorized scripts
                if (unauthorizedScripts.Any())
                {
                    foreach (var scriptUrl in unauthorizedScripts)
                    {
                        await _complianceAlertService.CreateUnauthorizedScriptAlertAsync(
                            store.Id,
                            scriptUrl,
                            request.PageUrl,
                            JsonSerializer.Serialize(new
                            {
                                SessionId = request.SessionId,
                                Context = request.Context,
                                DetectionMethod = "enhanced-client-monitoring",
                                PaymentScripts = request.PaymentScripts,
                                Timestamp = DateTime.UtcNow
                            })
                        );
                    }

                    await _logger.WarningAsync($"Enhanced monitoring detected {unauthorizedScripts.Count} unauthorized scripts on {request.PageUrl} - Session: {request.SessionId}");
                }

                return Json(new
                {
                    success = true,
                    logId = log.Id,
                    authorizedCount = authorizedCount,
                    unauthorizedCount = unauthorizedScripts.Count,
                    unauthorizedScripts = unauthorizedScripts
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error processing monitoring session report", ex);
                return Json(new { success = false, error = "Failed to process monitoring session" });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ReportAjaxMonitoring")]
        public async Task<IActionResult> ReportAjaxMonitoring([FromBody] AjaxMonitoringRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();

                // Process new scripts detected after AJAX - filter external only
                var externalScripts = _sriHelper.FilterExternalScripts(request.NewScripts, store);
                var authorizedCount = 0;
                var unauthorizedScripts = new List<string>();

                foreach (var scriptUrl in externalScripts)
                {
                    var (isAuthorized, _) = await _authorizedScriptService.IsScriptAuthorizedAsync(scriptUrl, store.Id);
                    if (isAuthorized)
                        authorizedCount++;
                    else
                        unauthorizedScripts.Add(scriptUrl);
                }

                // Count local scripts as authorized
                var localScriptsCount = request.NewScripts.Count - externalScripts.Count;
                authorizedCount += localScriptsCount;

                // Create monitoring log entry for AJAX-detected scripts
                var log = new ScriptMonitoringLog
                {
                    StoreId = store.Id,
                    PageUrl = request.PageUrl,
                    DetectedScripts = JsonSerializer.Serialize(request.NewScripts),
                    HttpHeaders = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["X-Detection-Method"] = "ajax-monitoring",
                        ["X-Ajax-Source"] = request.AjaxSource,
                        ["X-Session-Id"] = request.SessionId
                    }),
                    HasUnauthorizedScripts = unauthorizedScripts.Any(),
                    UnauthorizedScripts = JsonSerializer.Serialize(unauthorizedScripts),
                    CheckedOnUtc = DateTime.UtcNow,
                    CheckType = $"ajax-{request.AjaxSource}",
                    UserAgent = request.UserAgent,
                    TotalScriptsFound = request.NewScripts.Count,
                    AuthorizedScriptsCount = authorizedCount,
                    UnauthorizedScriptsCount = unauthorizedScripts.Count,
                    AlertSent = false
                };

                await _monitoringService.InsertMonitoringLogAsync(log);

                // Create alerts for unauthorized scripts
                if (unauthorizedScripts.Any())
                {
                    foreach (var scriptUrl in unauthorizedScripts)
                    {
                        await _complianceAlertService.CreateUnauthorizedScriptAlertAsync(
                            store.Id,
                            scriptUrl,
                            request.PageUrl,
                            JsonSerializer.Serialize(new
                            {
                                SessionId = request.SessionId,
                                AjaxSource = request.AjaxSource,
                                Context = request.Context,
                                DetectionMethod = "ajax-monitoring",
                                PreAjaxScripts = request.PreAjaxScripts,
                                Timestamp = DateTime.UtcNow
                            })
                        );
                    }
                }

                await _logger.InformationAsync($"AJAX monitoring detected {request.NewScripts.Count} new scripts ({unauthorizedScripts.Count} unauthorized) - Source: {request.AjaxSource}, Session: {request.SessionId}");

                return Json(new
                {
                    success = true,
                    logId = log.Id,
                    newScriptsCount = request.NewScripts.Count,
                    authorizedCount = authorizedCount,
                    unauthorizedCount = unauthorizedScripts.Count
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error processing AJAX monitoring report", ex);
                return Json(new { success = false, error = "Failed to process AJAX monitoring" });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ReportPaymentMethodMonitoring")]
        public async Task<IActionResult> ReportPaymentMethodMonitoring([FromBody] PaymentMethodMonitoringRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();

                // Process payment method scripts - filter external only
                var externalScripts = _sriHelper.FilterExternalScripts(request.PaymentScripts, store);
                var authorizedCount = 0;
                var unauthorizedScripts = new List<string>();

                foreach (var scriptUrl in externalScripts)
                {
                    var (isAuthorized, _) = await _authorizedScriptService.IsScriptAuthorizedAsync(scriptUrl, store.Id);
                    if (isAuthorized)
                        authorizedCount++;
                    else
                        unauthorizedScripts.Add(scriptUrl);
                }

                // Count local scripts as authorized
                var localScriptsCount = request.PaymentScripts.Count - externalScripts.Count;
                authorizedCount += localScriptsCount;

                // Create monitoring log specifically for payment method changes
                var log = new ScriptMonitoringLog
                {
                    StoreId = store.Id,
                    PageUrl = request.PageUrl,
                    DetectedScripts = JsonSerializer.Serialize(request.PaymentScripts),
                    HttpHeaders = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["X-Detection-Method"] = "payment-method-monitoring",
                        ["X-Payment-Method"] = request.PaymentMethod,
                        ["X-Session-Id"] = request.SessionId,
                        ["X-Payment-Context"] = request.Context
                    }),
                    HasUnauthorizedScripts = unauthorizedScripts.Any(),
                    UnauthorizedScripts = JsonSerializer.Serialize(unauthorizedScripts),
                    CheckedOnUtc = DateTime.UtcNow,
                    CheckType = $"payment-{request.PaymentMethod}",
                    UserAgent = request.UserAgent,
                    TotalScriptsFound = request.PaymentScripts.Count,
                    AuthorizedScriptsCount = authorizedCount,
                    UnauthorizedScriptsCount = unauthorizedScripts.Count,
                    AlertSent = false
                };

                await _monitoringService.InsertMonitoringLogAsync(log);

                // Enhanced alerts for payment-related violations
                if (unauthorizedScripts.Any())
                {
                    foreach (var scriptUrl in unauthorizedScripts)
                    {
                        await _complianceAlertService.CreateUnauthorizedScriptAlertAsync(
                            store.Id,
                            scriptUrl,
                            request.PageUrl,
                            JsonSerializer.Serialize(new
                            {
                                SessionId = request.SessionId,
                                PaymentMethod = request.PaymentMethod,
                                Context = request.Context,
                                DetectionMethod = "payment-method-monitoring",
                                SecurityImplication = "HIGH - Payment processing script",
                                Timestamp = DateTime.UtcNow
                            })
                        );
                    }

                    await _logger.WarningAsync($"CRITICAL: Unauthorized payment scripts detected for {request.PaymentMethod} - Scripts: {string.Join(", ", unauthorizedScripts)} - Session: {request.SessionId}");
                }

                return Json(new
                {
                    success = true,
                    logId = log.Id,
                    paymentMethod = request.PaymentMethod,
                    paymentScriptsCount = request.PaymentScripts.Count,
                    authorizedCount = authorizedCount,
                    unauthorizedCount = unauthorizedScripts.Count,
                    securityLevel = unauthorizedScripts.Any() ? "HIGH RISK" : "COMPLIANT"
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error processing payment method monitoring report", ex);
                return Json(new { success = false, error = "Failed to process payment method monitoring" });
            }
        }

        #endregion
    }
}