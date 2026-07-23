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

// Web também desenha/edita direto, sem pedir aprovação — mesma decisão do Android
// (PlataformaEdicaoDireta, já em Mobile.Core): o técnico segue tendo a palavra final
// porque pode mexer por cima depois no Android, não porque a Web precisa pedir licença.
builder.Services.AddScoped<IPlataformaEdicao, PlataformaEdicaoDireta>();

builder.Services.AddHttpClient<IApiClient, ApiClient>(cliente =>
{
    cliente.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<TokenAuthHandler>();

// Ícones técnicos e OCR (ferramenta de cota) da ferramenta de desenho — ver
// PlantaCanvasEdicaoWeb (Rendering/).
builder.Services.AddHttpClient<IconeSvgCatalogoWeb>(cliente =>
{
    cliente.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});
builder.Services.AddScoped<IOcrService, OcrServicoWeb>();

builder.Services.AddTransient<LoginViewModel>();
builder.Services.AddTransient<ProjetosViewModel>();
builder.Services.AddTransient<PlantasDoProjetoViewModel>();
builder.Services.AddTransient<PlantaViewModel>();

await builder.Build().RunAsync();
