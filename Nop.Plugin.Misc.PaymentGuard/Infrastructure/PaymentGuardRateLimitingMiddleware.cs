using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Services.Configuration;
using Nop.Services.Logging;

namespace Nop.Plugin.Misc.PaymentGuard.Infrastructure
{
    /// <summary>
    /// Rate limiting middleware for PaymentGuard API endpoints
    /// </summary>
    public class PaymentGuardRateLimitingMiddleware
    {
        #region Constants

        private static readonly CacheKey PAYMENTGUARD_RATELIMIT_KEY = new("Nop.paymentguard.ratelimit.{0}-{1}");

        #endregion

        #region Fields

        private readonly RequestDelegate _next;
        private readonly IStaticCacheManager _cacheManager;
        private readonly ILogger _logger;
        private static readonly ConcurrentDictionary<string, DateTime> _lastResetTime = new();

        #endregion

        #region Ctor

        public PaymentGuardRateLimitingMiddleware(RequestDelegate next,
            IStaticCacheManager cacheManager,
            ILogger logger)
        {
            _next = next;
            _cacheManager = cacheManager;
            _logger = logger;
        }

        #endregion

        #region Methods

        public async Task InvokeAsync(HttpContext context,
            IStoreContext storeContext,
            ISettingService settingService)
        {
            // Only apply rate limiting to PaymentGuard API endpoints
            if (!context.Request.Path.StartsWithSegments("/Plugins/PaymentGuard/Api"))
            {
                await _next(context);
                return;
            }

            try
            {
                var store = await storeContext.GetCurrentStoreAsync();
                var settings = await settingService.LoadSettingAsync<PaymentGuardSettings>(store.Id);

                if (!settings.EnableApiRateLimit)
                {
                    await _next(context);
                    return;
                }

                var clientIp = GetClientIpAddress(context);

                // Check if IP is whitelisted
                if (IsWhitelistedIp(clientIp, settings.WhitelistedIPs))
                {
                    await _next(context);
                    return;
                }

                // Check rate limit
                if (!await IsRequestAllowedAsync(clientIp, settings.ApiRateLimitPerHour))
                {
                    await _logger.WarningAsync($"PaymentGuard API rate limit exceeded for IP: {clientIp}");

                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.Headers.Add("Retry-After", "3600"); // 1 hour

                    await context.Response.WriteAsync("API rate limit exceeded. Please try again later.");
                    return;
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error in PaymentGuard rate limiting middleware", ex);
                // Don't block the request if there's an error in rate limiting
                await _next(context);
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get client IP address from request
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <returns>Client IP address</returns>
        private static string GetClientIpAddress(HttpContext context)
        {
            // Check for forwarded IP first (in case of proxy/load balancer)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
                return ips[0].Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
                return realIp.Trim();

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        /// <summary>
        /// Check if IP address is whitelisted
        /// </summary>
        /// <param name="clientIp">Client IP address</param>
        /// <param name="whitelistedIPs">Comma-separated list of whitelisted IPs</param>
        /// <returns>True if whitelisted</returns>
        private static bool IsWhitelistedIp(string clientIp, string whitelistedIPs)
        {
            if (string.IsNullOrEmpty(whitelistedIPs))
                return false;

            var whitelist = whitelistedIPs.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(ip => ip.Trim())
                .ToList();

            return whitelist.Contains(clientIp);
        }

        /// <summary>
        /// Check if request is allowed based on rate limiting
        /// </summary>
        /// <param name="clientIp">Client IP address</param>
        /// <param name="maxRequestsPerHour">Maximum requests per hour</param>
        /// <returns>True if request is allowed</returns>
        private async Task<bool> IsRequestAllowedAsync(string clientIp, int maxRequestsPerHour)
        {
            var currentHour = DateTime.UtcNow.ToString("yyyyMMddHH");

            var fullCacheKey = _cacheManager
                .PrepareKeyForDefaultCache(PAYMENTGUARD_RATELIMIT_KEY, clientIp, currentHour);
            fullCacheKey.CacheTime = 3600; // Cache for 1 hour to avoid frequent cache hits

            // Get current request count for this hour
            var requestCount = await _cacheManager.GetAsync<int>(fullCacheKey, async () =>
            {
                return 0;
            });

            // Increment request count
            requestCount++;

            // Check if limit exceeded
            if (requestCount > maxRequestsPerHour)
            {
                return false;
            }

            // Clean up old cache entries periodically
            if (requestCount == 1) // First request in this hour
            {
                await CleanupOldCacheEntriesAsync(clientIp);
            }

            return true;
        }

        /// <summary>
        /// Clean up old cache entries for rate limiting
        /// </summary>
        /// <param name="clientIp">Client IP address</param>
        private async Task CleanupOldCacheEntriesAsync(string clientIp)
        {
            try
            {
                // Clean up entries older than 2 hours
                var cutoffTime = DateTime.UtcNow.AddHours(-2);
                // Remove old hourly entries
                for (var i = 1; i <= 24; i++) // Check last 24 hours
                {
                    var oldHour = cutoffTime.AddHours(-i).ToString("yyyyMMddHH");
                    
                    var oldCacheKey = _cacheManager
                        .PrepareKeyForDefaultCache(PAYMENTGUARD_RATELIMIT_KEY, clientIp, oldHour);
                    await _cacheManager.RemoveAsync(oldCacheKey);
                }
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error cleaning up rate limiting cache entries", ex);
            }
        }

        #endregion
    }
}