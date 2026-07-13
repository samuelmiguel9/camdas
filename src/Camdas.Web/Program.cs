using Camdas.Mobile.Services;
using Camdas.Mobile.ViewModels;
using Camdas.Web;
using Camdas.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("Configure \"ApiBaseUrl\" em wwwroot/appsettings.json.");

builder.Services.AddScoped<ITokenStore, LocalStorageTokenStore>();
builder.Services.AddTransient<TokenAuthHandler>();

builder.Services.AddHttpClient<IApiClient, ApiClient>(cliente =>
{
    cliente.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<TokenAuthHandler>();

builder.Services.AddTransient<LoginViewModel>();
builder.Services.AddTransient<ProjetosViewModel>();
builder.Services.AddTransient<PlantasDoProjetoViewModel>();
builder.Services.AddTransient<PlantaViewModel>();

await builder.Build().RunAsync();
