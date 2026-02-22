using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.VibeVoice.Extensions;

/// <summary>
/// Extension methods for registering VibeVoice services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers VibeVoice TTS services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional action to configure <see cref="VibeVoiceOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVibeVoice(
        this IServiceCollection services,
        Action<VibeVoiceOptions>? configure = null)
    {
        var options = new VibeVoiceOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IVibeVoiceSynthesizer>(sp =>
        {
            var opts = sp.GetRequiredService<VibeVoiceOptions>();
            return new VibeVoiceSynthesizer(opts);
        });

        return services;
    }
}
