namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class CSPViolationData
    {
        public string BlockedURI { get; set; }
        public string ViolatedDirective { get; set; }
        public string OriginalPolicy { get; set; }
        public string EffectiveDirective { get; set; }
        public string SourceFile { get; set; }
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
    }
}