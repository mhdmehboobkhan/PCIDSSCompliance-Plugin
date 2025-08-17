using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Misc.PaymentGuard.Enums
{
    public enum ScriptSource
    {
        Internal = 5,
        ThirdParty = 10,
        PaymentGateway = 15,
        Analytics = 20,
        Marketing = 25
    }
}