using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Plugin.Misc.PaymentGuard.Dto;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    /// <summary>
    /// Model for compliance report view/export
    /// </summary>
    public record ComplianceReportModel : BaseNopModel
    {
        public ComplianceReport Report { get; set; } = new ComplianceReport();

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ComplianceReport.Fields.SearchDateFrom")]
        [UIHint("DateNullable")]
        public DateTime? FromDate { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ComplianceReport.Fields.SearchDateTo")]
        [UIHint("DateNullable")]
        public DateTime? ToDate { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.ComplianceReport.Fields.SearchStore")]
        public int SearchStoreId { get; set; }

        public string StoreName { get; set; }

        public int StoreId { get; set; }

        public DateTime GeneratedOnUtc { get; set; } = DateTime.UtcNow;

        // Report Summary Properties (derived from Report)
        public string ComplianceLevel => Report.ComplianceScore switch
        {
            >= 95 => "Excellent",
            >= 80 => "Good",
            >= 60 => "Fair",
            _ => "Needs Attention"
        };

        public string ComplianceLevelClass => Report.ComplianceScore switch
        {
            >= 95 => "success",
            >= 80 => "info",
            >= 60 => "warning",
            _ => "danger"
        };

        public double IssueRate => Report.TotalScriptsMonitored > 0
            ? (double)Report.UnauthorizedScriptsCount / Report.TotalScriptsMonitored * 100
            : 0;

        public string ReportPeriod
        {
            get
            {
                if (FromDate.HasValue && ToDate.HasValue)
                    return $"{FromDate.Value:MMM dd, yyyy} - {ToDate.Value:MMM dd, yyyy}";
                if (FromDate.HasValue)
                    return $"From {FromDate.Value:MMM dd, yyyy}";
                if (ToDate.HasValue)
                    return $"Until {ToDate.Value:MMM dd, yyyy}";
                return "All Time";
            }
        }

        // Additional Report Metrics
        public IList<ComplianceReportSection> ReportSections { get; set; } = new List<ComplianceReportSection>();

        public ComplianceRecommendations Recommendations { get; set; } = new ComplianceRecommendations();
        
        public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();
    }

    /// <summary>
    /// Represents a section in the compliance report
    /// </summary>
    public record ComplianceReportSection
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; } // "Pass", "Fail", "Warning"
        public string StatusClass => Status.ToLower() switch
        {
            "pass" => "success",
            "fail" => "danger",
            "warning" => "warning",
            _ => "secondary"
        };
        public IList<string> Details { get; set; } = new List<string>();
        public IList<string> Actions { get; set; } = new List<string>();
    }

    /// <summary>
    /// Compliance recommendations based on current state
    /// </summary>
    public record ComplianceRecommendations
    {
        public IList<string> HighPriorityActions { get; set; } = new List<string>();
        public IList<string> MediumPriorityActions { get; set; } = new List<string>();
        public IList<string> LowPriorityActions { get; set; } = new List<string>();
        public IList<string> BestPractices { get; set; } = new List<string>();

        public bool HasRecommendations =>
            HighPriorityActions.Any() ||
            MediumPriorityActions.Any() ||
            LowPriorityActions.Any() ||
            BestPractices.Any();
    }

    /// <summary>
    /// Search model for compliance reports
    /// </summary>
    public record ComplianceReportSearchModel : BaseSearchModel
    {
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchDateFrom")]
        public DateTime? SearchDateFrom { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchDateTo")]
        public DateTime? SearchDateTo { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.ReportFormat")]
        public string ReportFormat { get; set; } = "html";

        public IList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AvailableFormats { get; set; } =
            new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
    }
}