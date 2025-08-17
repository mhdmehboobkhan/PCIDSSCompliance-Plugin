using Nop.Core.Domain.Catalog;
using Nop.Data.Mapping;
using Nop.Plugin.Misc.PaymentGuard.Domain;

namespace Nop.Plugin.Misc.PaymentGuard.Data
{
    /// <summary>
    /// Base instance of backward compatibility of table naming
    /// </summary>
    public partial class BaseNameCompatibility : INameCompatibility
    {
        public Dictionary<Type, string> TableNames => new()
        {
            { typeof(AuthorizedScript), "PaymentGuard_AuthorizedScript" },
            { typeof(ComplianceAlert), "PaymentGuard_ComplianceAlert" },
            { typeof(ScriptMonitoringLog), "PaymentGuard_ScriptMonitoringLog" },
        };

        public Dictionary<(Type, string), string> ColumnName => new()
        {
        };
    }
}