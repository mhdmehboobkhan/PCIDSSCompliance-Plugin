using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Security;
using Nop.Services.Security;

namespace Nop.Plugin.Misc.PaymentGuard
{
    /// <summary>
    /// Represents PaymentGuard permission provider
    /// </summary>
    public partial class PaymentGuardPermissionProvider : IPermissionProvider
    {
        public static readonly PermissionRecord ManagePaymentGuard = new()
        {
            Name = "Admin area. Manage PaymentGuard",
            SystemName = "ManagePaymentGuard",
            Category = "Plugin"
        };

        public static readonly PermissionRecord ViewComplianceReports = new()
        {
            Name = "Admin area. View Compliance Reports",
            SystemName = "ViewPaymentGuardReports",
            Category = "Plugin"
        };

        public static readonly PermissionRecord ManageAuthorizedScripts = new()
        {
            Name = "Admin area. Manage Authorized Scripts",
            SystemName = "ManageAuthorizedScripts",
            Category = "Plugin"
        };

        public static readonly PermissionRecord ViewComplianceAlerts = new()
        {
            Name = "Admin area. View Compliance Alerts",
            SystemName = "ViewComplianceAlerts",
            Category = "Plugin"
        };

        /// <summary>
        /// Get permissions
        /// </summary>
        /// <returns>Permissions</returns>
        public virtual IEnumerable<PermissionRecord> GetPermissions()
        {
            return new[]
            {
                ManagePaymentGuard,
                ViewComplianceReports,
                ManageAuthorizedScripts,
                ViewComplianceAlerts
            };
        }

        /// <summary>
        /// Get default permissions
        /// </summary>
        /// <returns>Permissions</returns>
        public virtual HashSet<(string systemRoleName, PermissionRecord[] permissions)> GetDefaultPermissions()
        {
            return new HashSet<(string, PermissionRecord[])>
            {
                (
                    NopCustomerDefaults.AdministratorsRoleName,
                    new[]
                    {
                        ManagePaymentGuard,
                        ViewComplianceReports,
                        ManageAuthorizedScripts,
                        ViewComplianceAlerts
                    }
                )
            };
        }
    }
}