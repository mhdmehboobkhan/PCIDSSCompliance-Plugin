using Nop.Core;
using Nop.Plugin.Misc.PaymentGuard.Domain;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial interface IAuthorizedScriptService
    {
        Task<IPagedList<AuthorizedScript>> GetAllAuthorizedScriptsAsync(int storeId = 0,
            string scriptUrl = "", int riskLevelId = 0, string sourceType = "",
            bool? isActive = null, int pageIndex = 0, int pageSize = int.MaxValue);

        Task<AuthorizedScript> GetAuthorizedScriptByIdAsync(int scriptId);

        Task<AuthorizedScript> GetAuthorizedScriptByUrlAsync(string scriptUrl, int storeId);

        Task InsertAuthorizedScriptAsync(AuthorizedScript script);

        Task UpdateAuthorizedScriptAsync(AuthorizedScript script);

        Task DeleteAuthorizedScriptAsync(AuthorizedScript script);

        Task<(bool isAuthorized, AuthorizedScript script)> IsScriptAuthorizedAsync(string scriptUrl, int storeId);

        Task<IList<AuthorizedScript>> GetAuthorizedScriptsByDomainAsync(string domain, int storeId);

        Task<bool> ValidateScriptIntegrityAsync(string scriptUrl, string expectedHash);

        Task UpdateScriptHashAsync(int scriptId, string newHash);

        Task<IPagedList<AuthorizedScript>> GetExpiredScriptsAsync(int daysSinceLastVerified,
            int storeId, bool getOnlyTotalCount = false);
    }
}