using CommunityToolkit.Maui;
using Plugin.Maui.Audio;
using VoiceLabs.Maui.Services;

namespace VoiceLabs.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Backend URL — change this to your Python backend address
        var backendUrl = "http://localhost:5100";

        builder.Services.AddHttpClient<TtsService>(client =>
        {
            client.BaseAddress = new Uri(backendUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        builder.Services.AddSingleton(AudioManager.Current);
        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }
}
