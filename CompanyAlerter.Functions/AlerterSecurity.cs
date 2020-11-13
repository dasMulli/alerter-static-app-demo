using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CompanyAlerter.Functions
{
    public class AlerterSecurity
    {
        private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

        private readonly SecurityConfig securityConfig;

        private readonly string IdentityProvider;
        private readonly string AdditionalValidIssuer;

        private readonly ILogger<AlerterSecurity> logger;

        public AlerterSecurity(SecurityConfig securityConfig, ILogger<AlerterSecurity> logger)
        {
            this.securityConfig = securityConfig;
            this.logger = logger;
            AdditionalValidIssuer = $"https://sts.windows.net/{securityConfig.TenantId}/";
            IdentityProvider = $"https://login.microsoftonline.com/{securityConfig.TenantId}/v2.0/";

            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{IdentityProvider}.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true }
            );
        }

        public async Task<ClaimsPrincipal> ValidateTokenAsync(AuthenticationHeaderValue value)
        {
            if (value?.Scheme != "Bearer")
            {
                logger.LogInformation("Request does not contain a Bearer authentication scheme");
                return null;
            }

            var config = await _configurationManager.GetConfigurationAsync(CancellationToken.None);

            var validationParameter = new TokenValidationParameters
            {
                RequireSignedTokens = true,
                ValidAudience = securityConfig.Audience,
                ValidateAudience = true,
                ValidIssuers = new[] { IdentityProvider, AdditionalValidIssuer },
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                IssuerSigningKeys = config.SigningKeys
            };

            ClaimsPrincipal result = null;
            var tries = 0;

            while (result == null && tries <= 1)
            {
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    result = handler.ValidateToken(value.Parameter, validationParameter, out var token);
                }
                catch (SecurityTokenSignatureKeyNotFoundException ex)
                {
                    logger.LogError(ex, "Key not found while validating token: {Token}", value.Parameter);
                    // This exception is thrown if the signature key of the JWT could not be found.
                    // This could be the case when the issuer changed its signing keys, so we trigger a 
                    // refresh and retry validation.
                    _configurationManager.RequestRefresh();
                    tries++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unable to validate token - {OriginalMessage}", ex.Message);
                    logger.LogInformation("Original Token: {Token}", value.Parameter);
                    return null;
                }
            }

            logger.LogInformation("Successfully validated token");

            return result;
        }
    }
}
