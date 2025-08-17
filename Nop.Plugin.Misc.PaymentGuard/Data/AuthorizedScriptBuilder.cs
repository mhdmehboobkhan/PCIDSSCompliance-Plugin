using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.PaymentGuard.Domain;

namespace Nop.Plugin.Misc.PaymentGuard.Data
{
    public partial class AuthorizedScriptBuilder : NopEntityBuilder<AuthorizedScript>
    {
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(nameof(AuthorizedScript.ScriptUrl)).AsString(2000).NotNullable()
                .WithColumn(nameof(AuthorizedScript.ScriptHash)).AsString(128).Nullable()
                .WithColumn(nameof(AuthorizedScript.HashAlgorithm)).AsString(20).Nullable()
                .WithColumn(nameof(AuthorizedScript.Justification)).AsString(1000).Nullable()
                .WithColumn(nameof(AuthorizedScript.Purpose)).AsString(500).Nullable()
                .WithColumn(nameof(AuthorizedScript.AuthorizedBy)).AsString(100).Nullable()
                .WithColumn(nameof(AuthorizedScript.Source)).AsString(50).Nullable()
                .WithColumn(nameof(AuthorizedScript.Domain)).AsString(255).Nullable();
        }
    }
}