using BellucSketch.Mobile.Services;
using BellucSketch.Mobile.ViewModels;
using BellucSketch.Web;
using BellucSketch.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("Configure \"ApiBaseUrl\" em wwwroot/appsettings.json.");

builder.Services.AddScoped<ITokenStore, LocalStorageTokenStore>();
builder.Services.AddTransient<TokenAuthHandler>();

// PlantaViewModel e ProjetosViewModel (BellucSketch.Mobile.Core, compartilhadas com o app Android) exigem
// estes serviços por injeção de construtor — sem registrar algo aqui, o DI do Blazor falha ao criar
// a página inteira com uma exceção não tratada (tela de erro genérica do Blazor no navegador).
// BellucSketch.Web é só visualização, então ambos são no-ops aqui — ver comentário em cada classe.
builder.Services.AddSingleton<ISalvadorGaleria, SalvadorGaleriaNaoSuportado>();
builder.Services.AddSingleton<IVerificadorAtualizacao, VerificadorAtualizacaoNaoSuportado>();

// Web é auxiliar: edições de Camada viram propostas pendentes de aprovação de um técnico no Android
// (mestre) em vez de aplicar direto — ver PlataformaEdicaoWeb.
builder.Services.AddScoped<IPlataformaEdicao, PlataformaEdicaoWeb>();

builder.Services.AddHttpClient<IApiClient, ApiClient>(cliente =>
{
    cliente.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<TokenAuthHandler>();

builder.Services.AddTransient<LoginViewModel>();
builder.Services.AddTransient<ProjetosViewModel>();
builder.Services.AddTransient<PlantasDoProjetoViewModel>();
builder.Services.AddTransient<PlantaViewModel>();

await builder.Build().RunAsync();
