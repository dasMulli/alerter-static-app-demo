using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace CompanyAlerter.Shared
{
    public class AlerterSecurity
    {
        private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

        private readonly SecurityConfig securityConfig;

        private readonly string IdentityProvider;
        private readonly string AdditionalValidIssuer;

        public AlerterSecurity(SecurityConfig securityConfig)
        {
            this.securityConfig = securityConfig;
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
                return null;

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
                catch (SecurityTokenSignatureKeyNotFoundException)
                {
                    // This exception is thrown if the signature key of the JWT could not be found.
                    // This could be the case when the issuer changed its signing keys, so we trigger a 
                    // refresh and retry validation.
                    _configurationManager.RequestRefresh();
                    tries++;
                }
                catch (SecurityTokenException)
                {
                    return null;
                }
            }

            return result;
        }
    }
}
