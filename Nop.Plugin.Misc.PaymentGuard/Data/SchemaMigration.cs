using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.PaymentGuard.Domain;

namespace Nop.Plugin.Misc.PaymentGuard.Data
{
    [NopMigration("2025/01/20 11:05:01:6455424", "Misc.PaymentGuard base schema", MigrationProcessType.Installation)]
    public class SchemaMigration : Migration
    {
        public override void Up()
        {
            Create.TableFor<AuthorizedScript>();
            Create.TableFor<ComplianceAlert>();
            Create.TableFor<ScriptMonitoringLog>();
        }

        public override void Down()
        {
            Delete.Table("PaymentGuard_AuthorizedScript");
            Delete.Table("PaymentGuard_ComplianceAlert");
            Delete.Table("PaymentGuard_ScriptMonitoringLog");
        }
    }
}