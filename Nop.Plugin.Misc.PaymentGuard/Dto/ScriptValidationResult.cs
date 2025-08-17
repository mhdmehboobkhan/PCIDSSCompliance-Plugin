namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class ScriptValidationResult
    {
        public string ScriptUrl { get; set; }
        public bool IsAuthorized { get; set; }
        public bool HasValidSRI { get; set; }
        public SRIValidationResult SRIValidation { get; set; }
        public string StoredHash { get; set; }
        public string BrowserHash { get; set; }
        public bool HashMismatchDetected { get; set; }
        public bool ContentChangeDetected { get; set; }
        public DateTime? LastVerified { get; set; }
    }
}