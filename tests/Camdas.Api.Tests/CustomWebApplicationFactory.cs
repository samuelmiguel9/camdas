using Camdas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Camdas.Api.Tests;

/// <summary>
/// Host de testes de integração: troca o Npgsql real por Sqlite in-memory (sem precisar de um
/// PostgreSQL rodando) e fixa a configuração de JWT em valores conhecidos, para que os testes
/// consigam emitir tokens válidos diretamente com <see cref="Camdas.Api.Auth.JwtTokenGenerator"/>.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _conexao = new("DataSource=:memory:");
    private bool _conexaoAberta;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (!_conexaoAberta)
        {
            _conexao.Open();
            _conexaoAberta = true;
        }

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Chave"] = TestJwtSettings.Chave,
                ["Jwt:Emissor"] = TestJwtSettings.Emissor,
                ["Jwt:Audiencia"] = TestJwtSettings.Audiencia,
                ["Jwt:ExpiracaoMinutos"] = "60",
                ["ArmazenamentoArquivos:DiretorioRaiz"] = Path.Combine(Path.GetTempPath(), "camdas-testes-" + Guid.NewGuid()),
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CamdasDbContext>>();
            services.AddDbContext<CamdasDbContext>(options => options.UseSqlite(_conexao));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _conexao.Dispose();
    }
}
