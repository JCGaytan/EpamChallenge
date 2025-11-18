using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TextProcessor.Api.Tests.TestDoubles;
using TextProcessor.Core.Interfaces;

namespace TextProcessor.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                var signalRHealthCheck = options.Registrations
                    .FirstOrDefault(registration => registration.Name == "signalr-hub");

                if (signalRHealthCheck != null)
                {
                    options.Registrations.Remove(signalRHealthCheck);
                }
            });

            services.RemoveAll<ITextProcessingService>();
            services.AddScoped<ITextProcessingService, TestTextProcessingService>();
        });
    }
}
