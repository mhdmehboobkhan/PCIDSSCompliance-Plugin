using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.PaymentGuard.Domain;

namespace Nop.Plugin.Misc.PaymentGuard.Data
{
    public class ScriptMonitoringLogBuilder : NopEntityBuilder<ScriptMonitoringLog>
    {
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(nameof(ScriptMonitoringLog.PageUrl)).AsString(int.MaxValue).Nullable()
                .WithColumn(nameof(ScriptMonitoringLog.DetectedScripts)).AsString(int.MaxValue).Nullable()
                .WithColumn(nameof(ScriptMonitoringLog.HttpHeaders)).AsString(int.MaxValue).Nullable()
                .WithColumn(nameof(ScriptMonitoringLog.UnauthorizedScripts)).AsString(int.MaxValue).Nullable()
                .WithColumn(nameof(ScriptMonitoringLog.CheckType)).AsString(2000).Nullable()
                .WithColumn(nameof(ScriptMonitoringLog.UserAgent)).AsString(2000).Nullable();
        }
    }
}