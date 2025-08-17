using Nop.Core.Domain.Messages;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Dto;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial interface IEmailAlertService
    {

        /// <summary>
        /// Get token groups of message template
        /// </summary>
        /// <param name="messageTemplate">Message template</param>
        /// <returns>Collection of token group names</returns>
        IEnumerable<string> GetTokenGroups(MessageTemplate messageTemplate);

        /// <summary>
        /// Get collection of allowed (supported) message tokens
        /// </summary>
        /// <param name="tokenGroups">Collection of token groups; pass null to get all available tokens</param>
        /// <returns>Collection of allowed message tokens</returns>
        Task<IEnumerable<string>> GetListOfAllowedTokensAsync(IEnumerable<string> tokenGroups = null);

        Task<IList<int>> SendUnauthorizedScriptAlertAsync(ScriptMonitoringLog scriptMonitoringLog, int storeId, int languageId = 0);
        
        Task<IList<int>> SendComplianceReportAsync(ComplianceReport complianceReport, int storeId, int languageId = 0);
        
        Task<IList<int>> SendScriptChangeAlertAsync(string scriptUrl, int storeId, int languageId = 0);
        
        Task<IList<int>> SendCSPViolationAlertAsync(string violationDetails, int storeId, int languageId = 0);
        
        Task<IList<int>> SendExpiredScriptsAlertAsync(IList<AuthorizedScript> expiredScripts, int storeId, int languageId = 0);
        
        Task<IList<int>> SendBlockedScriptAlertAsync(string scriptUrl, string pageUrl, int storeId, int languageId = 0);
    }
}