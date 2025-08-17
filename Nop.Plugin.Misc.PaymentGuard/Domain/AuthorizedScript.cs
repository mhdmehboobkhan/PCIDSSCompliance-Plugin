using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.PaymentGuard.Enums;

namespace Nop.Plugin.Misc.PaymentGuard.Domain
{
    public partial class AuthorizedScript : BaseEntity
    {
        public string ScriptUrl { get; set; }

        public string ScriptHash { get; set; }

        public string HashAlgorithm { get; set; } = "sha384";

        public string Justification { get; set; }

        public string Purpose { get; set; }

        public int RiskLevelId { get; set; } = 1; // 1=Low, 2=Medium, 3=High

        public bool IsActive { get; set; } = true;

        public string AuthorizedBy { get; set; }

        public DateTime AuthorizedOnUtc { get; set; }

        public DateTime LastVerifiedUtc { get; set; }

        public string Source { get; set; } // "internal", "third-party", "payment-gateway"

        public string Domain { get; set; }

        public int StoreId { get; set; }

        #region Custom properties

        public RiskLevel RiskLevel
        {
            get => (RiskLevel)RiskLevelId;
            set => RiskLevelId = (int)value;
        }

        #endregion
    }
}