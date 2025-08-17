using Nop.Plugin.Misc.PaymentGuard.Dto;
using Nop.Plugin.Misc.PaymentGuard.Helpers;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public class SRIValidationService : ISRIValidationService
    {
        #region Fields

        private readonly SRIHelper _sriHelper;

        #endregion

        #region Ctor

        public SRIValidationService(SRIHelper sriHelper)
        {
            _sriHelper = sriHelper;
        }

        #endregion

        #region Methods
        
        /// <summary>
        /// Validate that a script's current hash matches its integrity attribute
        /// </summary>
        public async Task<SRIValidationResult> ValidateScriptIntegrityAsync(string scriptUrl, string expectedIntegrity)
        {
            try
            {
                // Generate current hash for the script
                var currentHash = await _sriHelper.GenerateExternalSRIHashAsync(scriptUrl);

                if (string.IsNullOrEmpty(currentHash))
                {
                    return new SRIValidationResult
                    {
                        IsValid = false,
                        Error = "Could not generate hash for script"
                    };
                }

                // Compare with expected integrity value
                var isMatch = string.Equals(currentHash, expectedIntegrity, StringComparison.OrdinalIgnoreCase);

                return new SRIValidationResult
                {
                    IsValid = isMatch,
                    CurrentHash = currentHash,
                    ExpectedHash = expectedIntegrity,
                    ScriptUrl = scriptUrl,
                    Error = isMatch ? null : "Hash mismatch - script may have been modified"
                };
            }
            catch (Exception ex)
            {
                return new SRIValidationResult
                {
                    IsValid = false,
                    Error = $"Validation failed: {ex.Message}"
                };
            }
        }
        
        #endregion
    }
}