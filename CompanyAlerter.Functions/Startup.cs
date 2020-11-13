using System;
using CompanyAlerter.Functions;
using CompanyAlerter.Shared;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;

[assembly: FunctionsStartup(typeof(Startup))]

namespace CompanyAlerter.Functions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var securityConfig = new SecurityConfig
            {
                TenantId = "ef660bca-495a-4ed8-88d3-38ce8741a9fa",
                Audience = "api://alerter-demo-preparation",
                AppId = "fc8265f9-b52d-4982-8461-2071c1e0afd5",
                ClientSecret = Environment.GetEnvironmentVariable("OAUTH_CLIENT_SECRET")
            };
            builder.Services.AddSingleton(securityConfig);
            builder.Services.AddSingleton<AlerterSecurity>();

            var confidentialClient = ConfidentialClientApplicationBuilder.Create(securityConfig.AppId).WithTenantId(securityConfig.TenantId)
                .WithClientSecret(securityConfig.ClientSecret).Build();
            var graphServiceClient = new GraphServiceClient(new ClientCredentialProvider(confidentialClient));
            builder.Services.AddSingleton<IGraphServiceClient>(graphServiceClient);
        }
    }
}
