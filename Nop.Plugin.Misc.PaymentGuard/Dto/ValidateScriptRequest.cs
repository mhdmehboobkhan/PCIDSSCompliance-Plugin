namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class ValidateScriptRequest
    {
        public string ScriptUrl { get; set; }
        public string PageUrl { get; set; }
        public string Timestamp { get; set; }
    }
}