namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class ValidateScriptWithSRIRequest
    {
        public string ScriptUrl { get; set; }
        public string Integrity { get; set; }
        public string PageUrl { get; set; }
        public string Context { get; set; }
        public string SessionId { get; set; }
        public string Timestamp { get; set; }
        public bool ForceValidation { get; set; } = false; // For scripts without integrity
    }
}