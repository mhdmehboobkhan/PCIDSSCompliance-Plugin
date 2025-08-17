using Nop.Plugin.Misc.PaymentGuard.Dto;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public interface ISRIValidationService
    {
        /// <summary>
        /// Validate that a script's current hash matches its integrity attribute
        /// </summary>
        Task<SRIValidationResult> ValidateScriptIntegrityAsync(string scriptUrl, string expectedIntegrity);
    }
}