using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using HKA_Handball.Services;

namespace HKA_Handball;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Audio plugin
        builder.AddAudio();

        // Sound manager (singleton so preloaded sounds persist)
        builder.Services.AddSingleton<SoundManager>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
