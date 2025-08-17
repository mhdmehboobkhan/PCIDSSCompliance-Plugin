namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    /// <summary>
    /// Represents token group names
    /// </summary>
    public static partial class EmailTokenGroupNames
    {
        /// <summary>
        /// Represents tokens with information about unauthorized scripts detection
        /// </summary>
        public const string UnauthorizedScriptTokens = "Unauthorized Script tokens";

        /// <summary>
        /// Represents tokens with information about compliance reports
        /// </summary>
        public const string ComplianceReportTokens = "Compliance Report tokens";

        /// <summary>
        /// Represents tokens with information about script changes
        /// </summary>
        public const string ScriptChangeTokens = "Script Change tokens";

        /// <summary>
        /// Represents tokens with information about CSP violations
        /// </summary>
        public const string CSPViolationTokens = "CSP Violation tokens";

        /// <summary>
        /// Represents tokens with information about expired scripts
        /// </summary>
        public const string ExpiredScriptTokens = "Expired Script tokens";

        /// <summary>
        /// Represents tokens with information about blocked scripts
        /// </summary>
        public const string BlockedScriptTokens = "Blocked Script tokens";
    }
}