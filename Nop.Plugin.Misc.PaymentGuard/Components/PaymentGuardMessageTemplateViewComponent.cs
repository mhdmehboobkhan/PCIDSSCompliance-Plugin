using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Areas.Admin.Models.Messages;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Misc.PaymentGuard.Components
{
    [ViewComponent(Name = "PaymentGuardMessageTemplate")]
    public class PaymentGuardMessageTemplateViewComponent : NopViewComponent
    {
        private readonly IMessageTokenProvider _messageTokenProvider;
        private readonly IMessageTemplateService _messageTemplateService;
        private readonly IEmailAlertService _emailAlertService;
        private readonly ILocalizationService _localizationService;

        public PaymentGuardMessageTemplateViewComponent(IMessageTokenProvider messageTokenProvider,
            IMessageTemplateService messageTemplateService,
            IEmailAlertService emailAlertService,
            ILocalizationService localizationService)
        {
            _messageTokenProvider = messageTokenProvider;
            _messageTemplateService = messageTemplateService;
            _emailAlertService = emailAlertService;
            _localizationService = localizationService;
        }

        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
        {
            var model = additionalData as MessageTemplateModel;
            if (model.Name == PaymentGuardDefaults.UnauthorizedScriptsAlertNotification || model.Name == PaymentGuardDefaults.ComplianceReportNotification
                || model.Name == PaymentGuardDefaults.ScriptChangeAlertNotification || model.Name == PaymentGuardDefaults.CSPViolationAlertNotification
                || model.Name == PaymentGuardDefaults.ExpiredScriptsAlertNotification || model.Name == PaymentGuardDefaults.BlockedScriptAlertNotification)
            {
                var messageTemplate = await _messageTemplateService.GetMessageTemplateByIdAsync(model.Id);
                if (messageTemplate != null)
                {
                    var allowedTokensList = new List<string>();
                    allowedTokensList.AddRange(await _messageTokenProvider.GetListOfAllowedTokensAsync(_emailAlertService.GetTokenGroups(messageTemplate)));
                    allowedTokensList.AddRange(await _emailAlertService.GetListOfAllowedTokensAsync(_emailAlertService.GetTokenGroups(messageTemplate)));

                    var allowedTokens = string.Join(", ", allowedTokensList);
                    model.AllowedTokens = $"{allowedTokens}{Environment.NewLine}{Environment.NewLine}" +
                        $"{await _localizationService.GetResourceAsync("Admin.ContentManagement.MessageTemplates.Tokens.ConditionalStatement")}{Environment.NewLine}";

                    return View(model);
                }
            }

            return Content("");
        }
    }
}