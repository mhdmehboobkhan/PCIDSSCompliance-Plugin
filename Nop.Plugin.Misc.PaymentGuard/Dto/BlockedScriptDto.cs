namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class BlockedScriptDto
    {
        public string ScriptUrl { get; set; }
        public string PageUrl { get; set; }
        public string Timestamp { get; set; }
        public string UserAgent { get; set; }
        public string BlockReason { get; set; }
    }
}