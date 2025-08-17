using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record AuthorizedScriptModel : BaseNopEntityModel
    {
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptUrl")]
        public string ScriptUrl { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.ScriptHash")]
        public string ScriptHash { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Purpose")]
        public string Purpose { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Justification")]
        public string Justification { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.RiskLevel")]
        public int RiskLevelId { get; set; }
        
        public string RiskLevelText { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Store")]
        public int StoreId { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.IsActive")]
        public bool IsActive { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.AuthorizedBy")]
        public string AuthorizedBy { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.AuthorizedOnUtc")]
        public DateTime AuthorizedOnUtc { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.LastVerifiedUtc")]
        public DateTime LastVerifiedUtc { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.Source")]
        public string Source { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ScriptManagement.Fields.GenerateHash")]
        public bool GenerateHash { get; set; }

        public IList<SelectListItem> AvailableRiskLevels { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableSources { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();

    }
}