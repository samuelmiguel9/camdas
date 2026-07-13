using Camdas.Mobile.Platforms.Services;
using Camdas.Mobile.Services;
using Camdas.Mobile.ViewModels;
using Camdas.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace Camdas.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        RegistrarServicos(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void RegistrarServicos(IServiceCollection services)
    {
        services.AddSingleton<IArmazenamentoEnderecosApi, ArmazenamentoEnderecosApiPreferences>();
        services.AddSingleton<ConfiguracaoApi>();
        services.AddSingleton<ResolvedorEnderecoApi>();
        services.AddSingleton<ITokenStore, TokenStoreSecureStorage>();
        services.AddTransient<TokenAuthHandler>();
        services.AddTransient<EnderecoDinamicoHandler>();

        services.AddHttpClient<IApiClient, ApiClient>((provedor, cliente) =>
        {
            var configuracao = provedor.GetRequiredService<ConfiguracaoApi>();
            cliente.BaseAddress = new Uri(configuracao.BaseUrl);
        }).AddHttpMessageHandler<EnderecoDinamicoHandler>()
          .AddHttpMessageHandler<TokenAuthHandler>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<ProjetosViewModel>();
        services.AddTransient<PlantasDoProjetoViewModel>();
        services.AddTransient<PlantaViewModel>();
        services.AddTransient<CamadaEdicaoViewModel>();
        services.AddTransient<HistoricoViewModel>();

        services.AddTransient<LoginPage>();
        services.AddTransient<ProjetosPage>();
        services.AddTransient<PlantasDoProjetoPage>();
        services.AddTransient<PlantaPage>();
        services.AddTransient<CamadaEdicaoPage>();
        services.AddTransient<HistoricoPage>();
    }
}
