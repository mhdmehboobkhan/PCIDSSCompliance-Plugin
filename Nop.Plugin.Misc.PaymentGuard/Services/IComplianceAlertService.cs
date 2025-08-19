using Nop.Core;
using Nop.Plugin.Misc.PaymentGuard.Domain;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial interface IComplianceAlertService
    {
        /// <summary>
        /// Get all compliance alerts
        /// </summary>
        /// <param name="storeId">Store identifier; 0 to load all records</param>
        /// <param name="alertType">Alert type filter</param>
        /// <param name="alertLevel">Alert level filter</param>
        /// <param name="isResolved">Resolved status filter</param>
        /// <param name="pageIndex">Page index</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Compliance alerts</returns>
        Task<IPagedList<ComplianceAlert>> GetAllComplianceAlertsAsync(int storeId = 0,
            string alertType = null,
            string alertLevel = null,
            bool? isResolved = null,
            int pageIndex = 0,
            int pageSize = int.MaxValue);

        /// <summary>
        /// Get compliance alert by identifier
        /// </summary>
        /// <param name="alertId">Alert identifier</param>
        /// <returns>Compliance alert</returns>
        Task<ComplianceAlert> GetComplianceAlertByIdAsync(int alertId);

        /// <summary>
        /// Insert compliance alert
        /// </summary>
        /// <param name="alert">Compliance alert</param>
        Task InsertComplianceAlertAsync(ComplianceAlert alert);

        /// <summary>
        /// Update compliance alert
        /// </summary>
        /// <param name="alert">Compliance alert</param>
        Task UpdateComplianceAlertAsync(ComplianceAlert alert);

        /// <summary>
        /// Delete compliance alert
        /// </summary>
        /// <param name="alert">Compliance alert</param>
        Task DeleteComplianceAlertAsync(ComplianceAlert alert);

        Task<ComplianceAlert> CreateSecurityAlertAsync(int storeId, string scriptUrl, string pageUrl, string details);


        /// <summary>
        /// Create unauthorized script alert
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="scriptUrl">Script URL</param>
        /// <param name="pageUrl">Page URL</param>
        /// <param name="details">Additional details</param>
        /// <returns>Created alert</returns>
        Task<ComplianceAlert> CreateUnauthorizedScriptAlertAsync(int storeId, string scriptUrl, string pageUrl, string details = null);

        /// <summary>
        /// Create CSP violation alert
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="pageUrl">Page URL</param>
        /// <param name="violationDetails">Violation details</param>
        /// <returns>Created alert</returns>
        Task<ComplianceAlert> CreateCSPViolationAlertAsync(int storeId, string pageUrl, string violationDetails);

        /// <summary>
        /// Create script integrity failure alert
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="scriptUrl">Script URL</param>
        /// <param name="pageUrl">Page URL</param>
        /// <param name="details">Additional details</param>
        /// <returns>Created alert</returns>
        Task<ComplianceAlert> CreateIntegrityFailureAlertAsync(int storeId, string scriptUrl, string pageUrl, string details = null);

        /// <summary>
        /// Resolve compliance alert
        /// </summary>
        /// <param name="alertId">Alert identifier</param>
        /// <param name="resolvedBy">User who resolved the alert</param>
        /// <returns>Updated alert</returns>
        Task<ComplianceAlert> ResolveAlertAsync(int alertId, string resolvedBy);

        /// <summary>
        /// Get unresolved alerts count by store
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <returns>Count of unresolved alerts</returns>
        Task<int> GetUnresolvedAlertsCountAsync(int storeId);

        /// <summary>
        /// Get recent alerts
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="hours">Hours to look back</param>
        /// <param name="maxResults">Maximum number of results</param>
        /// <returns>Recent alerts</returns>
        Task<IList<ComplianceAlert>> GetRecentAlertsAsync(int storeId, 
            int hours = 24, int maxResults = 10, string violationType = "", string scriptUrl = "");

        /// <summary>
        /// Check if similar alert exists recently
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="alertType">Alert type</param>
        /// <param name="scriptUrl">Script URL</param>
        /// <param name="pageUrl">Page URL</param>
        /// <param name="hoursBack">Hours to check back</param>
        /// <returns>True if similar alert exists</returns>
        Task<bool> HasRecentSimilarAlertAsync(int storeId, string alertType, string scriptUrl, string pageUrl, int hoursBack = 24);
    }
}