using System.Security.Cryptography;
using System.Text;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Data;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Helpers;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial class AuthorizedScriptService : IAuthorizedScriptService
    {
        #region Fields

        private readonly IRepository<AuthorizedScript> _authorizedScriptRepository;
        private readonly IStaticCacheManager _staticCacheManager;
        private readonly HttpClient _httpClient;
        private readonly SRIHelper _sriHelper;

        #endregion

        #region Ctor

        public AuthorizedScriptService(IRepository<AuthorizedScript> authorizedScriptRepository,
            IStaticCacheManager staticCacheManager,
            HttpClient httpClient,
            SRIHelper sriHelper)
        {
            _authorizedScriptRepository = authorizedScriptRepository;
            _staticCacheManager = staticCacheManager;
            _httpClient = httpClient;
            _sriHelper = sriHelper;
        }

        #endregion

        #region Constants

        /// <summary>
        /// Key for caching authorized scripts by store
        /// </summary>
        /// <remarks>
        /// {0} : store ID
        /// </remarks>
        private static readonly CacheKey AUTHORIZED_SCRIPTS_BY_STORE_KEY = new("Nop.paymentguard.authorizedscripts.bystore.{0}", AUTHORIZED_SCRIPTS_PATTERN_KEY);

        /// <summary>
        /// Key for caching individual script authorization status
        /// </summary>
        /// <remarks>
        /// {0} : script URL
        /// {1} : store ID
        /// </remarks>
        private static readonly CacheKey SCRIPT_AUTHORIZATION_KEY = new("Nop.paymentguard.script.authorized.{0}.{1}", AUTHORIZED_SCRIPTS_PATTERN_KEY);

        /// <summary>
        /// Key pattern to clear cache
        /// </summary>
        private const string AUTHORIZED_SCRIPTS_PATTERN_KEY = "Nop.paymentguard.authorizedscripts.";

        #endregion

        #region Utilities

        /// <summary>
        /// Generate a short hash for script URL to use in cache keys
        /// </summary>
        /// <param name="scriptUrl">Script URL</param>
        /// <returns>Short hash</returns>
        private static string GenerateScriptUrlHash(string scriptUrl)
        {
            using var sha1 = SHA1.Create();
            var hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(scriptUrl));
            return Convert.ToHexString(hashBytes)[..8]; // Take first 8 characters
        }

        #endregion

        #region Methods

        public virtual async Task<IPagedList<AuthorizedScript>> GetAllAuthorizedScriptsAsync(int storeId = 0,
            string scriptUrl = "", int riskLevelId = 0, string sourceType = "",
            bool? isActive = null, int pageIndex = 0, int pageSize = int.MaxValue)
        {
            var query = _authorizedScriptRepository.Table;

            if (storeId > 0)
                query = query.Where(script => script.StoreId == storeId);

            if (!string.IsNullOrEmpty(scriptUrl))
                query = query.Where(script => script.ScriptUrl.Contains(scriptUrl));

            if (riskLevelId > 0)
                query = query.Where(script => script.RiskLevelId == riskLevelId);

            if (!string.IsNullOrEmpty(sourceType))
                query = query.Where(script => script.Source.Contains(sourceType));

            if (storeId > 0)
                query = query.Where(script => script.StoreId == storeId);

            if (isActive.HasValue)
                query = query.Where(script => script.IsActive == isActive.Value);

            query = query.OrderByDescending(script => script.AuthorizedOnUtc);

            return await query.ToPagedListAsync(pageIndex, pageSize);
        }

        public virtual async Task<AuthorizedScript> GetAuthorizedScriptByIdAsync(int scriptId)
        {
            return await _authorizedScriptRepository.GetByIdAsync(scriptId);
        }

        public virtual async Task<AuthorizedScript> GetAuthorizedScriptByUrlAsync(string scriptUrl, int storeId)
        {
            if (string.IsNullOrEmpty(scriptUrl))
                return null;

            var query = _authorizedScriptRepository.Table
                .Where(script => script.ScriptUrl == scriptUrl && script.StoreId == storeId);

            return await query.FirstOrDefaultAsync();
        }

        public virtual async Task InsertAuthorizedScriptAsync(AuthorizedScript script)
        {
            ArgumentNullException.ThrowIfNull(script);

            await _authorizedScriptRepository.InsertAsync(script);

            // Clear cache
            await _staticCacheManager.RemoveByPrefixAsync(AUTHORIZED_SCRIPTS_PATTERN_KEY);
        }

        public virtual async Task UpdateAuthorizedScriptAsync(AuthorizedScript script)
        {
            ArgumentNullException.ThrowIfNull(script);

            await _authorizedScriptRepository.UpdateAsync(script);

            // Clear cache
            await _staticCacheManager.RemoveByPrefixAsync(AUTHORIZED_SCRIPTS_PATTERN_KEY);
        }

        public virtual async Task DeleteAuthorizedScriptAsync(AuthorizedScript script)
        {
            ArgumentNullException.ThrowIfNull(script);

            await _authorizedScriptRepository.DeleteAsync(script);

            // Clear cache
            await _staticCacheManager.RemoveByPrefixAsync(AUTHORIZED_SCRIPTS_PATTERN_KEY);
        }

        public virtual async Task<(bool isAuthorized, AuthorizedScript script)> IsScriptAuthorizedAsync(string scriptUrl, int storeId)
        {
            if (string.IsNullOrEmpty(scriptUrl))
                return (false, null);

            // Try to get from cache first
            var cacheKey = _staticCacheManager.PrepareKey(SCRIPT_AUTHORIZATION_KEY,
                GenerateScriptUrlHash(scriptUrl), storeId);

            var scriptObj = await _staticCacheManager.GetAsync(cacheKey, async () =>
            {
                var script = await GetAuthorizedScriptByUrlAsync(scriptUrl, storeId);
                return script;
            });

            return (scriptObj != null && scriptObj.IsActive, scriptObj);
        }

        public virtual async Task<IList<AuthorizedScript>> GetAuthorizedScriptsByDomainAsync(string domain, int storeId)
        {
            if (string.IsNullOrEmpty(domain))
                return new List<AuthorizedScript>();

            // Get from cache or database
            var allScripts = await _staticCacheManager.GetAsync(
                _staticCacheManager.PrepareKey(AUTHORIZED_SCRIPTS_BY_STORE_KEY, storeId),
                async () =>
                {
                    return await _authorizedScriptRepository.Table
                        .Where(script => script.StoreId == storeId && script.IsActive)
                        .ToListAsync();
                });

            return allScripts.Where(script => script.Domain == domain).ToList();
        }

        public virtual async Task<bool> ValidateScriptIntegrityAsync(string scriptUrl, string expectedHash)
        {
            var currentHash = await _sriHelper.GenerateExternalSRIHashAsync(scriptUrl);
            return currentHash != null && currentHash == expectedHash;
        }

        public virtual async Task UpdateScriptHashAsync(int scriptId, string newHash)
        {
            var script = await GetAuthorizedScriptByIdAsync(scriptId);
            if (script != null)
            {
                script.ScriptHash = newHash;
                script.LastVerifiedUtc = DateTime.UtcNow;
                await UpdateAuthorizedScriptAsync(script);
            }
        }

        public virtual async Task<IPagedList<AuthorizedScript>> GetExpiredScriptsAsync(int daysSinceLastVerified, 
            int storeId, bool getOnlyTotalCount = false)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysSinceLastVerified);

            var query = _authorizedScriptRepository.Table
                .Where(script => script.LastVerifiedUtc < cutoffDate && script.IsActive);

            query = query.Where(script => script.StoreId == storeId);

            return await query.ToPagedListAsync(0, int.MaxValue, getOnlyTotalCount);
        }

        #endregion
    }
}