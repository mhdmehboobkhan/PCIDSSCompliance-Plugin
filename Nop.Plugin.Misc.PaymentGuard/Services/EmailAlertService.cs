using Nop.Core;
using Nop.Services.Messages;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using System.Text.Json;
using Nop.Services.Helpers;
using Nop.Services.Logging;
using Nop.Core.Domain.Messages;
using DocumentFormat.OpenXml.Bibliography;
using Nop.Plugin.Misc.PaymentGuard.Dto;
using Nop.Services.Localization;
using Nop.Services.Stores;
using Nop.Core.Events;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial class EmailAlertService : IEmailAlertService
    {
        private readonly IEmailSender _emailSender;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly ILogger _logger;
        private readonly EmailAccountSettings _emailAccountSettings;
        private readonly IEmailAccountService _emailAccountService;
        private readonly IQueuedEmailService _queuedEmailService;
        private readonly ILocalizationService _localizationService;
        private readonly IMessageTemplateService _messageTemplateService;
        private readonly ILanguageService _languageService;
        private readonly IStoreService _storeService;
        private readonly IStoreContext _storeContext;
        private readonly IMessageTokenProvider _messageTokenProvider;
        private readonly IEventPublisher _eventPublisher;
        private readonly IWorkflowMessageService _workflowMessageService;

        private Dictionary<string, IEnumerable<string>> _allowedTokens;

        public EmailAlertService(IEmailSender emailSender,
            IDateTimeHelper dateTimeHelper,
            ILogger logger,
            EmailAccountSettings emailAccountSettings,
            IEmailAccountService emailAccountService,
            IQueuedEmailService queuedEmailService,
            ILocalizationService localizationService,
            IMessageTemplateService messageTemplateService,
            ILanguageService languageService,
            IStoreService storeService,
            IStoreContext storeContext,
            IMessageTokenProvider messageTokenProvider,
            IEventPublisher eventPublisher,
            IWorkflowMessageService workflowMessageService)
        {
            _emailSender = emailSender;
            _dateTimeHelper = dateTimeHelper;
            _logger = logger;
            _emailAccountSettings = emailAccountSettings;
            _emailAccountService = emailAccountService;
            _queuedEmailService = queuedEmailService;
            _localizationService = localizationService;
            _messageTemplateService = messageTemplateService;
            _languageService = languageService;
            _storeService = storeService;
            _storeContext = storeContext;
            _messageTokenProvider = messageTokenProvider;
            _eventPublisher = eventPublisher;
            _workflowMessageService = workflowMessageService;
        }

        #region Utilities

        /// <summary>
        /// Get EmailAccount to use with a message templates
        /// </summary>
        /// <param name="messageTemplate">Message template</param>
        /// <param name="languageId">Language identifier</param>
        /// <returns>EmailAccount</returns>
        protected virtual async Task<EmailAccount> GetEmailAccountOfMessageTemplateAsync(MessageTemplate messageTemplate, int languageId)
        {
            var emailAccountId = await _localizationService.GetLocalizedAsync(messageTemplate, mt => mt.EmailAccountId, languageId);
            //some 0 validation (for localizable "Email account" dropdownlist which saves 0 if "Standard" value is chosen)
            if (emailAccountId == 0)
            {
                emailAccountId = messageTemplate.EmailAccountId;
            }

            var emailAccount = (await _emailAccountService.GetEmailAccountByIdAsync(emailAccountId) ?? await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)) ??
                               (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();
            return emailAccount;
        }

        /// <summary>
        /// Get active message templates by the name
        /// </summary>
        /// <param name="messageTemplateName">Message template name</param>
        /// <param name="storeId">Store identifier</param>
        /// <returns>List of message templates</returns>
        protected virtual async Task<IList<MessageTemplate>> GetActiveMessageTemplatesAsync(string messageTemplateName, int storeId)
        {
            //get message templates by the name
            var messageTemplates = await _messageTemplateService.GetMessageTemplatesByNameAsync(messageTemplateName, storeId);

            //no template found
            if (!messageTemplates?.Any() ?? true)
                return new List<MessageTemplate>();

            //filter active templates
            messageTemplates = messageTemplates.Where(messageTemplate => messageTemplate.IsActive).ToList();

            return messageTemplates;
        }

        /// <summary>
        /// Ensure language is active
        /// </summary>
        /// <param name="languageId">Language identifier</param>
        /// <param name="storeId">Store identifier</param>
        /// <returns>Return a value language identifier</returns>
        protected virtual async Task<int> EnsureLanguageIsActiveAsync(int languageId, int storeId)
        {
            //load language by specified ID
            var language = await _languageService.GetLanguageByIdAsync(languageId);

            if (language == null || !language.Published)
            {
                //load any language from the specified store
                language = (await _languageService.GetAllLanguagesAsync(storeId: storeId)).FirstOrDefault();
            }

            if (language == null || !language.Published)
            {
                //load any language
                language = (await _languageService.GetAllLanguagesAsync()).FirstOrDefault();
            }

            if (language == null)
            {
                throw new Exception("No active language could be loaded");
            }

            return language.Id;
        }

        /// <summary>
        /// Gets allowed tokens for PaymentGuard email templates
        /// </summary>
        protected Dictionary<string, IEnumerable<string>> AllowedTokens
        {
            get
            {
                if (_allowedTokens != null)
                    return _allowedTokens;

                _allowedTokens = new Dictionary<string, IEnumerable<string>>();

                // Unauthorized Script Detection tokens
                _allowedTokens.Add(EmailTokenGroupNames.UnauthorizedScriptTokens, new[]
                {
                    "%PaymentGuard.PageUrl%",
                    "%PaymentGuard.CheckedTime%",
                    "%PaymentGuard.UnauthorizedScriptsList%",
                    "%PaymentGuard.TotalScriptsFound%",
                    "%PaymentGuard.AuthorizedScriptsCount%",
                    "%PaymentGuard.UnauthorizedScriptsCount%"
                });

                // Compliance Report tokens
                _allowedTokens.Add(EmailTokenGroupNames.ComplianceReportTokens, new[]
                {
                    "%PaymentGuard.ReportGeneratedTime%",
                    "%PaymentGuard.ComplianceScore%",
                    "%PaymentGuard.TotalScriptsMonitored%",
                    "%PaymentGuard.AuthorizedScriptsCount%",
                    "%PaymentGuard.UnauthorizedScriptsCount%",
                    "%PaymentGuard.TotalChecksPerformed%",
                    "%PaymentGuard.AlertsGenerated%",
                    "%PaymentGuard.LastCheckDate%",
                    "%PaymentGuard.MostCommonUnauthorizedScriptsSection%"
                });

                // Script Change Detection tokens
                _allowedTokens.Add(EmailTokenGroupNames.ScriptChangeTokens, new[]
                {
                    "%PaymentGuard.ScriptUrl%",
                    "%PaymentGuard.AlertTime%"
                });

                // CSP Violation tokens
                _allowedTokens.Add(EmailTokenGroupNames.CSPViolationTokens, new[]
                {
                    "%PaymentGuard.ViolationDetails%",
                    "%PaymentGuard.AlertTime%",
                });

                // Expired Scripts tokens
                _allowedTokens.Add(EmailTokenGroupNames.ExpiredScriptTokens, new[]
                {
                    "%PaymentGuard.ExpiredScriptsList%",
                    "%PaymentGuard.AlertTime%",
                });

                // Blocked Script tokens
                _allowedTokens.Add(EmailTokenGroupNames.BlockedScriptTokens, new[]
                {
                    "%PaymentGuard.ScriptUrl%",
                    "%PaymentGuard.PageUrl%",
                    "%PaymentGuard.AlertTime%",
                });

                return _allowedTokens;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Get token groups of message template
        /// </summary>
        /// <param name="messageTemplate">Message template</param>
        /// <returns>Collection of token group names</returns>
        public virtual IEnumerable<string> GetTokenGroups(MessageTemplate messageTemplate)
        {
            if (messageTemplate == null)
                throw new ArgumentNullException(nameof(messageTemplate));

            // Groups depend on which tokens are added at the appropriate methods in IPaymentGuardWorkflowMessageService
            switch (messageTemplate.Name)
            {
                case PaymentGuardDefaults.UnauthorizedScriptsAlertNotification:
                    return new[] 
                    { 
                        TokenGroupNames.StoreTokens, 
                        EmailTokenGroupNames.UnauthorizedScriptTokens,
                    };

                case PaymentGuardDefaults.ComplianceReportNotification:
                    return new[] 
                    { 
                        TokenGroupNames.StoreTokens, 
                        EmailTokenGroupNames.ComplianceReportTokens,
                    };

                case PaymentGuardDefaults.ScriptChangeAlertNotification:
                    return new[] 
                    { 
                        TokenGroupNames.StoreTokens, 
                        EmailTokenGroupNames.ScriptChangeTokens,
                    };

                case PaymentGuardDefaults.CSPViolationAlertNotification:
                    return new[] 
                    { 
                        TokenGroupNames.StoreTokens, 
                        EmailTokenGroupNames.CSPViolationTokens,
                    };

                case PaymentGuardDefaults.ExpiredScriptsAlertNotification:
                    return new[] 
                    { 
                        TokenGroupNames.StoreTokens, 
                        EmailTokenGroupNames.ExpiredScriptTokens,
                    };

                case PaymentGuardDefaults.BlockedScriptAlertNotification:
                    return new[] 
                    { 
                        TokenGroupNames.StoreTokens, 
                        EmailTokenGroupNames.BlockedScriptTokens,
                    };

                default:
                    return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Get collection of allowed (supported) message tokens
        /// </summary>
        /// <param name="tokenGroups">Collection of token groups; pass null to get all available tokens</param>
        /// <returns>Collection of allowed message tokens</returns>
        public virtual async Task<IEnumerable<string>> GetListOfAllowedTokensAsync(IEnumerable<string> tokenGroups = null)
        {
            var additionalTokens = new AdditionalTokensAddedEvent();
            await _eventPublisher.PublishAsync(additionalTokens);

            var allowedTokens = AllowedTokens.Where(x => tokenGroups == null || tokenGroups.Contains(x.Key))
                .SelectMany(x => x.Value).ToList();

            allowedTokens.AddRange(additionalTokens.AdditionalTokens);

            return allowedTokens.Distinct();
        }

        /// <summary>
        /// Sends unauthorized script alert notification
        /// </summary>
        /// <param name="scriptMonitoringLog">Script monitoring log</param>
        /// <param name="storeId">Store identifier</param>
        /// <param name="languageId">Language identifier</param>
        /// <returns>Queued email identifiers</returns>
        public virtual async Task<IList<int>> SendUnauthorizedScriptAlertAsync(ScriptMonitoringLog scriptMonitoringLog,
            int storeId, int languageId = 0)
        {
            if (scriptMonitoringLog == null)
                throw new ArgumentNullException(nameof(scriptMonitoringLog));

            var store = await _storeService.GetStoreByIdAsync(storeId) ?? await _storeContext.GetCurrentStoreAsync();
            languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

            var messageTemplates = await GetActiveMessageTemplatesAsync(PaymentGuardDefaults.UnauthorizedScriptsAlertNotification, store.Id);
            if (!messageTemplates.Any())
                return new List<int>();

            // Parse unauthorized scripts
            var unauthorizedScripts = JsonSerializer.Deserialize<List<string>>(scriptMonitoringLog.UnauthorizedScripts ?? "[]");
            var unauthorizedScriptsList = string.Join("", unauthorizedScripts.Select(script => $"<li><code>{script}</code></li>"));

            // Create common tokens
            var commonTokens = new List<Token>();
            commonTokens.Add(new Token("PaymentGuard.PageUrl", scriptMonitoringLog.PageUrl ?? string.Empty));
            commonTokens.Add(new Token("PaymentGuard.CheckedTime",
                (await _dateTimeHelper.ConvertToUserTimeAsync(scriptMonitoringLog.CheckedOnUtc, DateTimeKind.Utc))
                .ToString(CoreConstants.DEFAULT_DATETIME_FORMAT)));
            commonTokens.Add(new Token("PaymentGuard.UnauthorizedScriptsList", unauthorizedScriptsList, true));
            commonTokens.Add(new Token("PaymentGuard.TotalScriptsFound", scriptMonitoringLog.TotalScriptsFound.ToString()));
            commonTokens.Add(new Token("PaymentGuard.AuthorizedScriptsCount", scriptMonitoringLog.AuthorizedScriptsCount.ToString()));
            commonTokens.Add(new Token("PaymentGuard.UnauthorizedScriptsCount", scriptMonitoringLog.UnauthorizedScriptsCount.ToString()));

            return await messageTemplates.SelectAwait(async messageTemplate =>
            {
                var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

                var tokens = new List<Token>(commonTokens);
                await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount);

                // Event notification
                await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

                var toEmail = emailAccount.Email;
                var toName = emailAccount.DisplayName;

                return await _workflowMessageService.SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens,
                    toEmail, toName);
            }).ToListAsync();
        }

        /// <summary>
        /// Sends compliance report notification
        /// </summary>
        /// <param name="complianceReport">Compliance report</param>
        /// <param name="storeId">Store identifier</param>
        /// <param name="languageId">Language identifier</param>
        /// <returns>Queued email identifiers</returns>
        public virtual async Task<IList<int>> SendComplianceReportAsync(ComplianceReport complianceReport,
            int storeId, int languageId = 0)
        {
            if (complianceReport == null)
                throw new ArgumentNullException(nameof(complianceReport));

            var store = await _storeService.GetStoreByIdAsync(storeId) ?? await _storeContext.GetCurrentStoreAsync();
            languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

            var messageTemplates = await GetActiveMessageTemplatesAsync(PaymentGuardDefaults.ComplianceReportNotification, store.Id);
            if (!messageTemplates.Any())
                return new List<int>();

            // Build most common unauthorized scripts section
            var mostCommonSection = string.Empty;
            if (complianceReport.MostCommonUnauthorizedScripts?.Any() == true)
            {
                var scriptsList = string.Join("", complianceReport.MostCommonUnauthorizedScripts
                    .Select(script => $"<li><code>{script}</code></li>"));
                mostCommonSection = $@"<h3>Most Common Unauthorized Scripts:</h3><ul>{scriptsList}</ul>";
            }

            // Create common tokens
            var commonTokens = new List<Token>();
            commonTokens.Add(new Token("PaymentGuard.ReportGeneratedTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"));
            commonTokens.Add(new Token("PaymentGuard.ComplianceScore", complianceReport.ComplianceScore.ToString("F1")));
            commonTokens.Add(new Token("PaymentGuard.TotalScriptsMonitored", complianceReport.TotalScriptsMonitored.ToString()));
            commonTokens.Add(new Token("PaymentGuard.AuthorizedScriptsCount", complianceReport.AuthorizedScriptsCount.ToString()));
            commonTokens.Add(new Token("PaymentGuard.UnauthorizedScriptsCount", complianceReport.UnauthorizedScriptsCount.ToString()));
            commonTokens.Add(new Token("PaymentGuard.TotalChecksPerformed", complianceReport.TotalChecksPerformed.ToString()));
            commonTokens.Add(new Token("PaymentGuard.AlertsGenerated", complianceReport.AlertsGenerated.ToString()));
            commonTokens.Add(new Token("PaymentGuard.LastCheckDate", complianceReport.LastCheckDate.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"));
            commonTokens.Add(new Token("PaymentGuard.MostCommonUnauthorizedScriptsSection", mostCommonSection, true));

            return await messageTemplates.SelectAwait(async messageTemplate =>
            {
                var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);
                
                var tokens = new List<Token>(commonTokens);
                await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount);

                // Event notification
                await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

                var toEmail = emailAccount.Email;
                var toName = emailAccount.DisplayName;

                return await _workflowMessageService.SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens,
                    toEmail, toName);
            }).ToListAsync();
        }

        /// <summary>
        /// Sends script change alert notification
        /// </summary>
        /// <param name="scriptUrl">Changed script URL</param>
        /// <param name="storeId">Store identifier</param>
        /// <param name="languageId">Language identifier</param>
        /// <returns>Queued email identifiers</returns>
        public virtual async Task<IList<int>> SendScriptChangeAlertAsync(string scriptUrl,
            int storeId, int languageId = 0)
        {
            if (string.IsNullOrEmpty(scriptUrl))
                throw new ArgumentException("Script URL cannot be null or empty", nameof(scriptUrl));

            var store = await _storeService.GetStoreByIdAsync(storeId) ?? await _storeContext.GetCurrentStoreAsync();
            languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

            var messageTemplates = await GetActiveMessageTemplatesAsync(PaymentGuardDefaults.ScriptChangeAlertNotification, store.Id);
            if (!messageTemplates.Any())
                return new List<int>();

            // Create common tokens
            var commonTokens = new List<Token>();
            commonTokens.Add(new Token("PaymentGuard.ScriptUrl", scriptUrl));
            commonTokens.Add(new Token("PaymentGuard.AlertTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"));

            return await messageTemplates.SelectAwait(async messageTemplate =>
            {
                var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);
                
                var tokens = new List<Token>(commonTokens);
                await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount);

                // Event notification
                await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

                var toEmail = emailAccount.Email;
                var toName = emailAccount.DisplayName;

                return await _workflowMessageService.SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens,
                    toEmail, toName);
            }).ToListAsync();
        }

        /// <summary>
        /// Sends CSP violation alert notification
        /// </summary>
        /// <param name="violationDetails">CSP violation details</param>
        /// <param name="storeId">Store identifier</param>
        /// <param name="languageId">Language identifier</param>
        /// <returns>Queued email identifiers</returns>
        public virtual async Task<IList<int>> SendCSPViolationAlertAsync(string violationDetails,
            int storeId, int languageId = 0)
        {
            if (string.IsNullOrEmpty(violationDetails))
                throw new ArgumentException("Violation details cannot be null or empty", nameof(violationDetails));

            var store = await _storeService.GetStoreByIdAsync(storeId) ?? await _storeContext.GetCurrentStoreAsync();
            languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

            var messageTemplates = await GetActiveMessageTemplatesAsync(PaymentGuardDefaults.CSPViolationAlertNotification, store.Id);
            if (!messageTemplates.Any())
                return new List<int>();

            // Create common tokens
            var commonTokens = new List<Token>();
            commonTokens.Add(new Token("PaymentGuard.ViolationDetails", violationDetails));
            commonTokens.Add(new Token("PaymentGuard.AlertTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"));

            return await messageTemplates.SelectAwait(async messageTemplate =>
            {
                var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);
                
                var tokens = new List<Token>(commonTokens);
                await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount);

                // Event notification
                await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

                var toEmail = emailAccount.Email;
                var toName = emailAccount.DisplayName;

                return await _workflowMessageService.SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens,
                    toEmail, toName);
            }).ToListAsync();
        }

        /// <summary>
        /// Sends expired scripts alert notification
        /// </summary>
        /// <param name="expiredScripts">List of expired scripts</param>
        /// <param name="storeId">Store identifier</param>
        /// <param name="languageId">Language identifier</param>
        /// <returns>Queued email identifiers</returns>
        public virtual async Task<IList<int>> SendExpiredScriptsAlertAsync(IList<AuthorizedScript> expiredScripts,
            int storeId, int languageId = 0)
        {
            if (expiredScripts == null || !expiredScripts.Any())
                throw new ArgumentException("Expired scripts list cannot be null or empty", nameof(expiredScripts));

            var store = await _storeService.GetStoreByIdAsync(storeId) ?? await _storeContext.GetCurrentStoreAsync();
            languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

            var messageTemplates = await GetActiveMessageTemplatesAsync(PaymentGuardDefaults.ExpiredScriptsAlertNotification, store.Id);
            if (!messageTemplates.Any())
                return new List<int>();

            // Build expired scripts list
            var expiredScriptsList = string.Join("", expiredScripts.Select(s =>
                $"<li><code>{s.ScriptUrl}</code> - Last verified: {s.LastVerifiedUtc:yyyy-MM-dd}</li>"));

            // Create common tokens
            var commonTokens = new List<Token>();
            commonTokens.Add(new Token("PaymentGuard.ExpiredScriptsList", expiredScriptsList, true));
            commonTokens.Add(new Token("PaymentGuard.AlertTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"));

            return await messageTemplates.SelectAwait(async messageTemplate =>
            {
                var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);
                
                var tokens = new List<Token>(commonTokens);
                await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount);

                // Event notification
                await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

                var toEmail = emailAccount.Email;
                var toName = emailAccount.DisplayName;

                return await _workflowMessageService.SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens,
                    toEmail, toName);
            }).ToListAsync();
        }

        /// <summary>
        /// Sends blocked script alert notification
        /// </summary>
        /// <param name="scriptUrl">Blocked script URL</param>
        /// <param name="pageUrl">Page URL where script was blocked</param>
        /// <param name="storeId">Store identifier</param>
        /// <param name="languageId">Language identifier</param>
        /// <returns>Queued email identifiers</returns>
        public virtual async Task<IList<int>> SendBlockedScriptAlertAsync(string scriptUrl, string pageUrl,
            int storeId, int languageId = 0)
        {
            if (string.IsNullOrEmpty(scriptUrl))
                throw new ArgumentException("Script URL cannot be null or empty", nameof(scriptUrl));

            var store = await _storeService.GetStoreByIdAsync(storeId) ?? await _storeContext.GetCurrentStoreAsync();
            languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

            var messageTemplates = await GetActiveMessageTemplatesAsync(PaymentGuardDefaults.BlockedScriptAlertNotification, store.Id);
            if (!messageTemplates.Any())
                return new List<int>();

            // Create common tokens
            var commonTokens = new List<Token>();
            commonTokens.Add(new Token("PaymentGuard.ScriptUrl", scriptUrl));
            commonTokens.Add(new Token("PaymentGuard.PageUrl", pageUrl ?? string.Empty));
            commonTokens.Add(new Token("PaymentGuard.AlertTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"));

            return await messageTemplates.SelectAwait(async messageTemplate =>
            {
                var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);
                
                var tokens = new List<Token>(commonTokens);
                await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount);

                // Event notification
                await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

                var toEmail = emailAccount.Email;
                var toName = emailAccount.DisplayName;

                return await _workflowMessageService.SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens,
                    toEmail, toName);
            }).ToListAsync();
        }

        #endregion
    }
}