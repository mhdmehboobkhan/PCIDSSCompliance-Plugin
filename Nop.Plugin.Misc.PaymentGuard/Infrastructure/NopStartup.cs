using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.PaymentGuard.Helpers;
using Nop.Plugin.Misc.PaymentGuard.Services;

namespace Nop.Plugin.Misc.PaymentGuard.Infrastructure
{
    /// <summary>
    /// Represents object for the configuring services on application startup
    /// </summary>
    /// <seealso cref="Nop.Core.Infrastructure.INopStartup" />
    public class NopStartup : INopStartup
    {
        #region Methods

        /// <summary>
        /// Add and configure any of the middleware
        /// </summary>
        /// <param name="services">Collection of service descriptors</param>
        /// <param name="configuration">Configuration of the application</param>
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            //register custom services
            services.AddScoped<IAuthorizedScriptService, AuthorizedScriptService>();
            services.AddScoped<IMonitoringService, MonitoringService>();
            services.AddScoped<IComplianceAlertService, ComplianceAlertService>();
            services.AddScoped<IEmailAlertService, EmailAlertService>();
            services.AddScoped<IExportService, ExportService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<ISRIValidationService, SRIValidationService>();

            // Register SRI helper
            services.AddScoped<SRIHelper>();

            //register HttpClient for script fetching
            services.AddHttpClient<AuthorizedScriptService>();
            services.AddHttpClient<MonitoringService>();

            //themes support
            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.ViewLocationExpanders.Add(new ThemeablePluginViewLocationExpander());
            });
        }

        /// <summary>
        /// Configure the using of added middleware
        /// </summary>
        /// <param name="application">Builder for configuring an application's request pipeline</param>
        public void Configure(IApplicationBuilder application)
        {
            // Add rate limiting middleware for PaymentGuard API endpoints
            application.UseMiddleware<PaymentGuardRateLimitingMiddleware>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets order of this startup configuration implementation
        /// </summary>
        public int Order => 3000;

        #endregion
    }
}