using Nop.Plugin.Misc.PaymentGuard.Domain;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial interface IExportService
    {
        /// <summary>
        /// Export compliance alerts to CSV
        /// </summary>
        /// <param name="alerts">Compliance alerts</param>
        /// <returns>CSV data as byte array</returns>
        Task<byte[]> ExportComplianceAlertsToCsvAsync(IList<ComplianceAlert> alerts);

        /// <summary>
        /// Export monitoring logs to CSV
        /// </summary>
        /// <param name="logs">Monitoring logs</param>
        /// <returns>CSV data as byte array</returns>
        Task<byte[]> ExportMonitoringLogsToCsvAsync(IList<ScriptMonitoringLog> logs);

        /// <summary>
        /// Export authorized scripts to CSV
        /// </summary>
        /// <param name="scripts">Authorized scripts</param>
        /// <returns>CSV data as byte array</returns>
        Task<byte[]> ExportAuthorizedScriptsToCsvAsync(IList<AuthorizedScript> scripts);

        /// <summary>
        /// Generate compliance report PDF
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="fromDate">From date</param>
        /// <param name="toDate">To date</param>
        /// <returns>PDF data as byte array</returns>
        Task<byte[]> GenerateComplianceReportPdfAsync(int storeId, DateTime? fromDate = null, DateTime? toDate = null);
    }
}