using Microsoft.Extensions.DependencyInjection;
using TextProcessor.Core.Interfaces;
using TextProcessor.Core.Services;

namespace TextProcessor.Core.Extensions;

/// <summary>
/// Extension methods for registering core services with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core text processing services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddTextProcessingCore(this IServiceCollection services)
    {
        // Register core services
        services.AddScoped<ITextProcessingService, TextProcessingService>();
        services.AddSingleton<IJobManager, InMemoryJobManager>();
        
        return services;
    }
}