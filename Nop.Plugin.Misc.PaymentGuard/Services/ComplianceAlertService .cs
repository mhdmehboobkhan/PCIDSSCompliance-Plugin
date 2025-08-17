using System.Text.Json;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.PaymentGuard.Domain;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial class ComplianceAlertService : IComplianceAlertService
    {
        #region Fields

        private readonly IRepository<ComplianceAlert> _complianceAlertRepository;

        #endregion

        #region Ctor

        public ComplianceAlertService(IRepository<ComplianceAlert> complianceAlertRepository)
        {
            _complianceAlertRepository = complianceAlertRepository;
        }

        #endregion

        #region Methods

        public virtual async Task<IPagedList<ComplianceAlert>> GetAllComplianceAlertsAsync(int storeId = 0,
            string alertType = null,
            string alertLevel = null,
            bool? isResolved = null,
            int pageIndex = 0,
            int pageSize = int.MaxValue)
        {
            var query = _complianceAlertRepository.Table;

            if (storeId > 0)
                query = query.Where(alert => alert.StoreId == storeId);

            if (!string.IsNullOrEmpty(alertType))
                query = query.Where(alert => alert.AlertType == alertType);

            if (!string.IsNullOrEmpty(alertLevel))
                query = query.Where(alert => alert.AlertLevel == alertLevel);

            if (isResolved.HasValue)
                query = query.Where(alert => alert.IsResolved == isResolved.Value);

            query = query.OrderByDescending(alert => alert.CreatedOnUtc);

            return await query.ToPagedListAsync(pageIndex, pageSize);
        }

        public virtual async Task<ComplianceAlert> GetComplianceAlertByIdAsync(int alertId)
        {
            return await _complianceAlertRepository.GetByIdAsync(alertId);
        }

        public virtual async Task InsertComplianceAlertAsync(ComplianceAlert alert)
        {
            ArgumentNullException.ThrowIfNull(alert);

            await _complianceAlertRepository.InsertAsync(alert);
        }

        public virtual async Task UpdateComplianceAlertAsync(ComplianceAlert alert)
        {
            ArgumentNullException.ThrowIfNull(alert);

            await _complianceAlertRepository.UpdateAsync(alert);
        }

        public virtual async Task DeleteComplianceAlertAsync(ComplianceAlert alert)
        {
            ArgumentNullException.ThrowIfNull(alert);

            await _complianceAlertRepository.DeleteAsync(alert);
        }

        public virtual async Task<ComplianceAlert> CreateSecurityAlertAsync(int storeId, string scriptUrl, string pageUrl, string details)
        {
            var alert = new ComplianceAlert
            {
                StoreId = storeId,
                AlertType = "security-alert",
                AlertLevel = "critical",
                Message = $"Missing SRI detected: {scriptUrl}",
                Details = details,
                ScriptUrl = scriptUrl,
                PageUrl = pageUrl,
                IsResolved = false,
                CreatedOnUtc = DateTime.UtcNow,
                ResolvedOnUtc = null
            };

            await InsertComplianceAlertAsync(alert);
            return alert;
        }

        public virtual async Task<ComplianceAlert> CreateUnauthorizedScriptAlertAsync(int storeId, string scriptUrl, string pageUrl, string details = null)
        {
            // Check if similar alert exists recently to avoid spam
            if (await HasRecentSimilarAlertAsync(storeId, "unauthorized-script", scriptUrl, pageUrl, 1))
                return null;

            var alert = new ComplianceAlert
            {
                StoreId = storeId,
                AlertType = "unauthorized-script",
                AlertLevel = "critical",
                Message = $"Unauthorized script detected: {scriptUrl}",
                Details = details ?? JsonSerializer.Serialize(new
                {
                    ScriptUrl = scriptUrl,
                    PageUrl = pageUrl,
                    DetectedAt = DateTime.UtcNow,
                    UserAgent = "Unknown" // Note: HttpContext access should be handled at controller level
                }),
                ScriptUrl = scriptUrl,
                PageUrl = pageUrl,
                IsResolved = false,
                CreatedOnUtc = DateTime.UtcNow,
                EmailSent = false
            };

            await InsertComplianceAlertAsync(alert);
            return alert;
        }

        public virtual async Task<ComplianceAlert> CreateCSPViolationAlertAsync(int storeId, string pageUrl, string violationDetails)
        {
            // Check if similar alert exists recently
            if (await HasRecentSimilarAlertAsync(storeId, "csp-violation", null, pageUrl, 1))
                return null;

            var alert = new ComplianceAlert
            {
                StoreId = storeId,
                AlertType = "csp-violation",
                AlertLevel = "warning",
                Message = $"Content Security Policy violation on {pageUrl}",
                Details = violationDetails,
                PageUrl = pageUrl,
                IsResolved = false,
                CreatedOnUtc = DateTime.UtcNow,
                EmailSent = false
            };

            await InsertComplianceAlertAsync(alert);
            return alert;
        }

        public virtual async Task<ComplianceAlert> CreateIntegrityFailureAlertAsync(int storeId, string scriptUrl, string pageUrl, string details = null)
        {
            // Check if similar alert exists recently
            if (await HasRecentSimilarAlertAsync(storeId, "integrity-failure", scriptUrl, pageUrl, 1))
                return null;

            var alert = new ComplianceAlert
            {
                StoreId = storeId,
                AlertType = "integrity-failure",
                AlertLevel = "critical",
                Message = $"Script integrity failure: {scriptUrl}",
                Details = details ?? JsonSerializer.Serialize(new
                {
                    ScriptUrl = scriptUrl,
                    PageUrl = pageUrl,
                    FailedAt = DateTime.UtcNow
                }),
                ScriptUrl = scriptUrl,
                PageUrl = pageUrl,
                IsResolved = false,
                CreatedOnUtc = DateTime.UtcNow,
                EmailSent = false
            };

            await InsertComplianceAlertAsync(alert);
            return alert;
        }

        public virtual async Task<ComplianceAlert> ResolveAlertAsync(int alertId, string resolvedBy)
        {
            var alert = await GetComplianceAlertByIdAsync(alertId);
            if (alert == null || alert.IsResolved)
                return alert;

            alert.IsResolved = true;
            alert.ResolvedOnUtc = DateTime.UtcNow;
            alert.ResolvedBy = resolvedBy;

            await UpdateComplianceAlertAsync(alert);
            return alert;
        }

        public virtual async Task<int> GetUnresolvedAlertsCountAsync(int storeId)
        {
            var query = _complianceAlertRepository.Table
                .Where(alert => alert.StoreId == storeId && !alert.IsResolved);

            return await query.CountAsync();
        }

        public virtual async Task<IList<ComplianceAlert>> GetRecentAlertsAsync(int storeId, int hours = 24, int maxResults = 10)
        {
            var cutoffDate = DateTime.UtcNow.AddHours(-hours);

            var query = _complianceAlertRepository.Table
                .Where(alert => alert.StoreId == storeId && alert.CreatedOnUtc >= cutoffDate)
                .OrderByDescending(alert => alert.CreatedOnUtc)
                .Take(maxResults);

            return await query.ToListAsync();
        }

        public virtual async Task<bool> HasRecentSimilarAlertAsync(int storeId, string alertType, string scriptUrl, string pageUrl, int hoursBack = 24)
        {
            var cutoffDate = DateTime.UtcNow.AddHours(-hoursBack);

            var query = _complianceAlertRepository.Table
                .Where(alert => alert.StoreId == storeId
                    && alert.AlertType == alertType
                    && alert.CreatedOnUtc >= cutoffDate);

            if (!string.IsNullOrEmpty(scriptUrl))
                query = query.Where(alert => alert.ScriptUrl == scriptUrl);

            if (!string.IsNullOrEmpty(pageUrl))
                query = query.Where(alert => alert.PageUrl == pageUrl);

            return await query.AnyAsync();
        }

        #endregion
    }
}