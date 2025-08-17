using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.PaymentGuard.Domain;

namespace Nop.Plugin.Misc.PaymentGuard.Data
{
    public partial class ComplianceAlertBuilder : NopEntityBuilder<ComplianceAlert>
    {
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(nameof(ComplianceAlert.AlertType)).AsString(50).Nullable()
                .WithColumn(nameof(ComplianceAlert.AlertLevel)).AsString(20).Nullable()
                .WithColumn(nameof(ComplianceAlert.Message)).AsString(1000).Nullable()
                .WithColumn(nameof(ComplianceAlert.Details)).AsString(int.MaxValue).Nullable()
                .WithColumn(nameof(ComplianceAlert.ScriptUrl)).AsString(2000).Nullable()
                .WithColumn(nameof(ComplianceAlert.PageUrl)).AsString(2000).Nullable()
                .WithColumn(nameof(ComplianceAlert.ResolvedBy)).AsString(100).Nullable();
        }
    }
}