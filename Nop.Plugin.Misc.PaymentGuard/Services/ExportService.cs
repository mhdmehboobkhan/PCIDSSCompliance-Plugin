using System.Text;
using System.Text.Json;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Dto;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial class ExportService : IExportService
    {
        #region Fields

        private readonly IMonitoringService _monitoringService;
        private readonly IStoreService _storeService;

        #endregion

        #region Ctor

        public ExportService(
            IMonitoringService monitoringService,
            IStoreService storeService)
        {
            _monitoringService = monitoringService;
            _storeService = storeService;
        }

        #endregion

        #region Utilities

        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Escape quotes and handle multiline values
            return value.Replace("\"", "\"\"");
        }

        private void AddSummaryRow(PdfPTable table, string label, string value)
        {
            var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 11);

            PdfPCell cell1 = new PdfPCell(new Phrase(label, boldFont));
            cell1.BackgroundColor = new BaseColor(242, 242, 242); // light gray
            cell1.Padding = 5;
            table.AddCell(cell1);

            PdfPCell cell2 = new PdfPCell(new Phrase(value, normalFont));
            cell2.Padding = 5;
            table.AddCell(cell2);
        }

        #endregion

        #region Methods

        public virtual async Task<byte[]> ExportComplianceAlertsToCsvAsync(IList<ComplianceAlert> alerts)
        {
            var csv = new StringBuilder();

            // CSV Headers
            csv.AppendLine("Id,AlertType,AlertLevel,Message,ScriptUrl,PageUrl,IsResolved,CreatedOnUtc,ResolvedOnUtc,ResolvedBy,EmailSent");

            // CSV Data
            foreach (var alert in alerts)
            {
                csv.AppendLine($"{alert.Id}," +
                              $"\"{EscapeCsvValue(alert.AlertType)}\"," +
                              $"\"{EscapeCsvValue(alert.AlertLevel)}\"," +
                              $"\"{EscapeCsvValue(alert.Message)}\"," +
                              $"\"{EscapeCsvValue(alert.ScriptUrl)}\"," +
                              $"\"{EscapeCsvValue(alert.PageUrl)}\"," +
                              $"{alert.IsResolved}," +
                              $"{alert.CreatedOnUtc:yyyy-MM-dd HH:mm:ss}," +
                              $"{alert.ResolvedOnUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""}," +
                              $"\"{EscapeCsvValue(alert.ResolvedBy)}\"," +
                              $"{alert.EmailSent}");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public virtual async Task<byte[]> ExportMonitoringLogsToCsvAsync(IList<ScriptMonitoringLog> logs)
        {
            var csv = new StringBuilder();

            // CSV Headers
            csv.AppendLine("Id,PageUrl,TotalScriptsFound,AuthorizedScriptsCount,UnauthorizedScriptsCount,HasUnauthorizedScripts,CheckedOnUtc,CheckType,AlertSent");

            // CSV Data
            foreach (var log in logs)
            {
                csv.AppendLine($"{log.Id}," +
                              $"\"{EscapeCsvValue(log.PageUrl)}\"," +
                              $"{log.TotalScriptsFound}," +
                              $"{log.AuthorizedScriptsCount}," +
                              $"{log.UnauthorizedScriptsCount}," +
                              $"{log.HasUnauthorizedScripts}," +
                              $"{log.CheckedOnUtc:yyyy-MM-dd HH:mm:ss}," +
                              $"\"{EscapeCsvValue(log.CheckType)}\"," +
                              $"{log.AlertSent}");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public virtual async Task<byte[]> ExportAuthorizedScriptsToCsvAsync(IList<AuthorizedScript> scripts)
        {
            var csv = new StringBuilder();

            // CSV Headers
            csv.AppendLine("Id,ScriptUrl,Purpose,Justification,RiskLevel,Source,Domain,IsActive,AuthorizedBy,AuthorizedOnUtc,LastVerifiedUtc");

            // CSV Data
            foreach (var script in scripts)
            {
                csv.AppendLine($"{script.Id}," +
                              $"\"{EscapeCsvValue(script.ScriptUrl)}\"," +
                              $"\"{EscapeCsvValue(script.Purpose)}\"," +
                              $"\"{EscapeCsvValue(script.Justification)}\"," +
                              $"{script.RiskLevelId}," +
                              $"\"{EscapeCsvValue(script.Source)}\"," +
                              $"\"{EscapeCsvValue(script.Domain)}\"," +
                              $"{script.IsActive}," +
                              $"\"{EscapeCsvValue(script.AuthorizedBy)}\"," +
                              $"{script.AuthorizedOnUtc:yyyy-MM-dd HH:mm:ss}," +
                              $"{script.LastVerifiedUtc:yyyy-MM-dd HH:mm:ss}");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public virtual async Task<byte[]> GenerateComplianceReportPdfAsync(int storeId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var store = await _storeService.GetStoreByIdAsync(storeId);
            var storeName = store?.Name ?? "";
            var report = await _monitoringService.GenerateComplianceReportAsync(storeId, fromDate, toDate);

            using (var memoryStream = new MemoryStream())
            {
                // Create PDF Document
                Document document = new Document(PageSize.A4, 25, 25, 25, 25);
                PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                // --- Header ---
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                var subTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 11);

                Paragraph title = new Paragraph("PaymentGuard Compliance Report", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 10
                };
                document.Add(title);

                if (!string.IsNullOrEmpty(storeName))
                {
                    Paragraph storeNameParagraph = new Paragraph(storeName, subTitleFont)
                    {
                        Alignment = Element.ALIGN_CENTER,
                        SpacingAfter = 5
                    };
                    document.Add(storeNameParagraph);
                }

                Paragraph generatedDate = new Paragraph($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC", normalFont)
                {
                    Alignment = Element.ALIGN_CENTER
                };
                document.Add(generatedDate);

                if (fromDate.HasValue || toDate.HasValue)
                {
                    Paragraph period = new Paragraph($"Period: {fromDate?.ToString("yyyy-MM-dd") ?? "Beginning"} to {toDate?.ToString("yyyy-MM-dd") ?? "End"}", normalFont)
                    {
                        Alignment = Element.ALIGN_CENTER
                    };
                    document.Add(period);
                }

                document.Add(new Paragraph("\n"));

                // --- Compliance Summary Table ---
                Paragraph summaryHeader = new Paragraph("Compliance Summary", subTitleFont)
                {
                    SpacingAfter = 10
                };
                document.Add(summaryHeader);

                PdfPTable summaryTable = new PdfPTable(2) { WidthPercentage = 100 };
                summaryTable.SetWidths(new float[] { 40f, 60f });

                AddSummaryRow(summaryTable, "Compliance Score", $"{report.ComplianceScore:F1}%");
                AddSummaryRow(summaryTable, "Total Scripts Monitored", report.TotalScriptsMonitored.ToString());
                AddSummaryRow(summaryTable, "Authorized Scripts", report.AuthorizedScriptsCount.ToString());
                AddSummaryRow(summaryTable, "Unauthorized Scripts", report.UnauthorizedScriptsCount.ToString());
                AddSummaryRow(summaryTable, "Total Checks Performed", report.TotalChecksPerformed.ToString());
                AddSummaryRow(summaryTable, "Alerts Generated", report.AlertsGenerated.ToString());
                AddSummaryRow(summaryTable, "Last Check Date", report.LastCheckDate.ToString("yyyy-MM-dd HH:mm:ss"));

                document.Add(summaryTable);

                // --- Most Common Unauthorized Scripts ---
                if (report.MostCommonUnauthorizedScripts.Any())
                {
                    document.Add(new Paragraph("\nMost Common Unauthorized Scripts", subTitleFont));

                    foreach (var script in report.MostCommonUnauthorizedScripts)
                    {
                        document.Add(new Paragraph($"• {script}", normalFont));
                    }
                }

                document.Close();
                return memoryStream.ToArray();
            }
        }

        #endregion
    }
}