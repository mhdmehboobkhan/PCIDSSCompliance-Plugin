using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record MonitoringLogSearchModel : BaseSearchModel
    {
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchPageUrl")]
        public string SearchPageUrl { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchHasUnauthorizedScripts")]
        public string SearchHasUnauthorizedScripts { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchDateFrom")]
        public DateTime? SearchDateFrom { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchDateTo")]
        public DateTime? SearchDateTo { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.MonitoringLogs.Fields.SearchStoreId")]
        public int SearchStoreId { get; set; }

        public IList<SelectListItem> AvailableUnauthorizedOptions { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();
    }
}