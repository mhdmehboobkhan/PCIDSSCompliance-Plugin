using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record AuthorizedScriptSearchModel : BaseSearchModel
    {
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchScriptUrl")]
        public string SearchScriptUrl { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchIsActive")]
        public int SearchIsActiveId { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchRiskLevel")]
        public int SearchRiskLevelId { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchSource")]
        public string SearchSource { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.SearchStore")]
        public int SearchStoreId { get; set; }

        public IList<SelectListItem> AvailableActiveOptions { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableRiskLevels { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableSources { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();
    }
}