namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class SRIValidationResult
    {
        public bool IsValid { get; set; }
        public string ScriptUrl { get; set; }
        public string CurrentHash { get; set; }
        public string ExpectedHash { get; set; }
        public string Error { get; set; }
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    }
}