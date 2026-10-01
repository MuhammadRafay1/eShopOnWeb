using System;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Startup validation that <see cref="PayPalOptions.Environment"/> names an environment this SDK supports.
/// This SDK version ships only a Sandbox environment constant, so any other configured value is a deployment
/// fault and must fail boot with a clear message naming the config key — rather than silently defaulting or
/// throwing a confusing runtime error. Credential presence is enforced separately via DataAnnotations.
/// </summary>
public sealed class PayPalOptionsValidator : IValidateOptions<PayPalOptions>
{
    public ValidateOptionsResult Validate(string? name, PayPalOptions options)
    {
        if (!string.Equals(options.Environment, "Sandbox", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                $"PayPal:Environment is '{options.Environment}', but this integration's SDK only supports 'Sandbox'. " +
                "Set PayPal:Environment (PAYPAL_ENVIRONMENT) to 'Sandbox'.");
        }

        return ValidateOptionsResult.Success;
    }
}
