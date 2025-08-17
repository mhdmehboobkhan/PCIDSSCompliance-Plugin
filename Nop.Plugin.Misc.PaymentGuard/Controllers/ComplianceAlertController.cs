using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.Misc.PaymentGuard.Models;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.PaymentGuard.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    [AutoValidateAntiforgeryToken]
    public class ComplianceAlertController : BasePluginController
    {
        #region Fields

        private readonly IComplianceAlertService _complianceAlertService;
        private readonly IStoreService _storeService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly INotificationService _notificationService;
        private readonly ILocalizationService _localizationService;
        private readonly IPermissionService _permissionService;
        private readonly IExportService _exportService;
        private readonly ILogger _logger;
        private readonly IBaseAdminModelFactory _baseAdminModelFactory;

        #endregion

        #region Ctor

        public ComplianceAlertController(
            IComplianceAlertService complianceAlertService,
            IStoreService storeService,
            IStoreContext storeContext,
            IWorkContext workContext,
            INotificationService notificationService,
            ILocalizationService localizationService,
            IPermissionService permissionService,
            IExportService exportService,
            ILogger logger,
            IBaseAdminModelFactory baseAdminModelFactory)
        {
            _complianceAlertService = complianceAlertService;
            _storeService = storeService;
            _storeContext = storeContext;
            _workContext = workContext;
            _notificationService = notificationService;
            _localizationService = localizationService;
            _permissionService = permissionService;
            _exportService = exportService;
            _logger = logger;
            _baseAdminModelFactory = baseAdminModelFactory;
        }

        #endregion

        #region Methods

        public async Task<IActionResult> List(string searchIsResolved = "", string searchAlertLevel = "")
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return AccessDeniedView();

            var currentStore = await _storeContext.GetCurrentStoreAsync();
            var model = new ComplianceAlertSearchModel()
            {
                SearchIsResolved = searchIsResolved,
                SearchAlertLevel = searchAlertLevel,
                SearchStoreId = currentStore.Id
            };

            // Prepare dropdown lists
            await _baseAdminModelFactory.PrepareStoresAsync(model.AvailableStores, false);
            
            model.AvailableAlertTypes = new List<SelectListItem>
            {
                new() { Value = "", Text = await _localizationService.GetResourceAsync("Admin.Common.All") },
                new() { Value = "unauthorized-script", Text = "Unauthorized Script" },
                new() { Value = "csp-violation", Text = "CSP Violation" },
                new() { Value = "integrity-failure", Text = "Integrity Failure" }
            };

            model.AvailableAlertLevels = new List<SelectListItem>
            {
                new() { Value = "", Text = await _localizationService.GetResourceAsync("Admin.Common.All") },
                new() { Value = "info", Text = "Info" },
                new() { Value = "warning", Text = "Warning" },
                new() { Value = "critical", Text = "Critical" }
            };

            model.AvailableResolvedOptions = new List<SelectListItem>
            {
                new() { Value = "", Text = await _localizationService.GetResourceAsync("Admin.Common.All") },
                new() { Value = "false", Text = "Unresolved" },
                new() { Value = "true", Text = "Resolved" }
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> List(ComplianceAlertSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return await AccessDeniedDataTablesJson();

            bool? isResolved = null;
            if (!string.IsNullOrEmpty(searchModel.SearchIsResolved) && bool.TryParse(searchModel.SearchIsResolved, out var resolved))
                isResolved = resolved;

            var alerts = await _complianceAlertService.GetAllComplianceAlertsAsync(storeId: searchModel.SearchStoreId,
                alertType: searchModel.SearchAlertType,
                alertLevel: searchModel.SearchAlertLevel,
                isResolved: isResolved,
                pageIndex: searchModel.Page - 1,
                pageSize: searchModel.PageSize);

            var model = new ComplianceAlertListModel().PrepareToGrid(searchModel, alerts, () =>
            {
                return alerts.Select(alert => new ComplianceAlertModel
                {
                    Id = alert.Id,
                    AlertType = alert.AlertType,
                    AlertLevel = alert.AlertLevel,
                    Message = alert.Message,
                    ScriptUrl = alert.ScriptUrl,
                    PageUrl = alert.PageUrl,
                    IsResolved = alert.IsResolved,
                    CreatedOnUtc = alert.CreatedOnUtc,
                    ResolvedOnUtc = alert.ResolvedOnUtc,
                    ResolvedBy = alert.ResolvedBy,
                    EmailSent = alert.EmailSent
                });
            });

            return Json(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return AccessDeniedView();

            var alert = await _complianceAlertService.GetComplianceAlertByIdAsync(id);
            if (alert == null)
                return RedirectToAction("List");

            var model = new ComplianceAlertModel
            {
                Id = alert.Id,
                AlertType = alert.AlertType,
                AlertLevel = alert.AlertLevel,
                Message = alert.Message,
                Details = alert.Details,
                ScriptUrl = alert.ScriptUrl,
                PageUrl = alert.PageUrl,
                IsResolved = alert.IsResolved,
                CreatedOnUtc = alert.CreatedOnUtc,
                ResolvedOnUtc = alert.ResolvedOnUtc,
                ResolvedBy = alert.ResolvedBy,
                EmailSent = alert.EmailSent,
                EmailSentOnUtc = alert.EmailSentOnUtc
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Resolve(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return AccessDeniedView();

            var currentUser = await _workContext.GetCurrentCustomerAsync();
            var alert = await _complianceAlertService.ResolveAlertAsync(id, currentUser.Email);

            if (alert != null)
            {
                _notificationService.SuccessNotification("Alert has been resolved successfully.");
            }
            else
            {
                _notificationService.ErrorNotification("Alert not found or already resolved.");
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return AccessDeniedView();

            var alert = await _complianceAlertService.GetComplianceAlertByIdAsync(id);
            if (alert != null)
            {
                await _complianceAlertService.DeleteComplianceAlertAsync(alert);
                _notificationService.SuccessNotification("Alert has been deleted successfully.");
            }

            return Json(new { success = true });
        }

        #endregion

        #region Bulk Operations

        [HttpPost]
        public async Task<IActionResult> BulkResolveAlerts(IList<int> alertIds)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return Json(new { success = false, message = "Access denied" });

            try
            {
                if (alertIds == null || !alertIds.Any())
                    return Json(new { success = false, message = "No alerts selected" });

                var currentUser = await _workContext.GetCurrentCustomerAsync();
                var resolvedCount = 0;

                foreach (var alertId in alertIds)
                {
                    var alert = await _complianceAlertService.ResolveAlertAsync(alertId, currentUser.Email);
                    if (alert != null)
                        resolvedCount++;
                }

                return Json(new
                {
                    success = true,
                    message = $"Successfully resolved {resolvedCount} alert(s)",
                    resolvedCount = resolvedCount
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error in bulk resolve alerts operation", ex);
                return Json(new { success = false, message = "Error resolving alerts" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BulkDeleteAlerts(IList<int> alertIds)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return Json(new { success = false, message = "Access denied" });

            try
            {
                if (alertIds == null || !alertIds.Any())
                    return Json(new { success = false, message = "No alerts selected" });

                var deletedCount = 0;

                foreach (var alertId in alertIds)
                {
                    var alert = await _complianceAlertService.GetComplianceAlertByIdAsync(alertId);
                    if (alert != null)
                    {
                        await _complianceAlertService.DeleteComplianceAlertAsync(alert);
                        deletedCount++;
                    }
                }

                return Json(new
                {
                    success = true,
                    message = $"Successfully deleted {deletedCount} alert(s)",
                    deletedCount = deletedCount
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error in bulk delete alerts operation", ex);
                return Json(new { success = false, message = "Error deleting alerts" });
            }
        }

        #endregion

        #region Export Actions

        [HttpPost]
        public async Task<IActionResult> ExportAlertsToCsv(ComplianceAlertSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return AccessDeniedView();

            try
            {
                bool? isResolved = null;
                if (!string.IsNullOrEmpty(searchModel.SearchIsResolved) && bool.TryParse(searchModel.SearchIsResolved, out var resolved))
                    isResolved = resolved;

                var alerts = await _complianceAlertService.GetAllComplianceAlertsAsync(
                    storeId: searchModel.SearchStoreId,
                    alertType: searchModel.SearchAlertType,
                    alertLevel: searchModel.SearchAlertLevel,
                    isResolved: isResolved);

                var csvData = await _exportService.ExportComplianceAlertsToCsvAsync(alerts.ToList());

                var fileName = $"compliance-alerts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                return File(csvData, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error exporting compliance alerts to CSV", ex);
                _notificationService.ErrorNotification("Error exporting data to CSV");
                return RedirectToAction("List", "ComplianceAlert");
            }
        }

        #endregion
    }
}