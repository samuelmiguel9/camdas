using System.Text;
using Amazon.S3;
using Camdas.Api.Auth;
using Camdas.Api.Middleware;
using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using Camdas.Application.Projetos;
using Camdas.Infrastructure;
using Camdas.Infrastructure.Import;
using Camdas.Infrastructure.Persistence;
using Camdas.Infrastructure.Repositories;
using Camdas.Infrastructure.Storage;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Logging estruturado (Serilog) ---
// Configurado inteiramente via appsettings.json (seção "Serilog"), para poder trocar sink/nível por
// ambiente (ex.: appsettings.Production.json) sem recompilar.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// --- Persistência ---
builder.Services.AddDbContext<CamdasDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Camdas")));

// --- Portas da Application -> implementações da Infrastructure ---
builder.Services.AddScoped<IProjetoRepository, ProjetoRepositoryEfCore>();
builder.Services.AddScoped<IPlantaRepository, PlantaRepositoryEfCore>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepositoryEfCore>();
builder.Services.AddScoped<IHistoricoRepository, HistoricoRepositoryEfCore>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWorkEfCore>();
builder.Services.AddScoped<IConversorPdfParaImagem, ConversorPdfParaImagemPdfium>();
builder.Services.AddSingleton<IClock, RelogioSistema>();

// "S3" usa um bucket compatível com S3 (ex.: Supabase Storage, Cloudflare R2) — necessário em hosts
// com disco efêmero (Render free, por exemplo), onde ArquivoStorageEmDisco perderia os arquivos a
// cada deploy/reinício. Ver GUIA_DEPLOY_RENDER.md.
if (string.Equals(builder.Configuration["ArmazenamentoArquivos:Tipo"], "S3", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
        builder.Configuration["ArmazenamentoArquivos:S3:AccessKey"],
        builder.Configuration["ArmazenamentoArquivos:S3:SecretKey"],
        new AmazonS3Config
        {
            ServiceURL = builder.Configuration["ArmazenamentoArquivos:S3:EndpointUrl"],
            ForcePathStyle = true, // exigido por endpoints S3-compatíveis fora da AWS
        }));
    builder.Services.AddSingleton<IArquivoStorage>(provedor => new ArquivoStorageS3(
        provedor.GetRequiredService<IAmazonS3>(),
        builder.Configuration["ArmazenamentoArquivos:S3:Bucket"] ?? "plantas"));
}
else
{
    builder.Services.AddSingleton<IArquivoStorage>(_ =>
        new ArquivoStorageEmDisco(builder.Configuration["ArmazenamentoArquivos:DiretorioRaiz"] ?? "App_Data/plantas"));
}

// --- Identidade do usuário autenticado (lida do JWT) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioContext, UsuarioContextHttp>();

// --- JWT (emissão de token — ver aviso em AutenticacaoController) ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// --- MediatR + FluentValidation ---
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<CriarProjetoCommand>();
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssemblyContaining<CriarProjetoCommand>();

// --- Tratamento global de erros ---
builder.Services.AddExceptionHandler<TratadorDeExcecoesGlobal>();
builder.Services.AddProblemDetails();

// --- Autenticação/Autorização ---
// JwtBearerOptions é configurado de forma tardia (via IOptions<JwtOptions>, resolvido pelo DI só na
// primeira requisição) em vez de ler builder.Configuration antecipadamente aqui — assim usa
// exatamente a mesma leitura de configuração que IJwtTokenGenerator usa para assinar o token,
// evitando qualquer divergência entre "config no momento do Program.cs" vs "config no momento da
// requisição" (isso causava um 401 "signature key was not found" nos testes de integração).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
    {
        var jwt = jwtOptionsAccessor.Value;

        // Sem isso, o handler renomeia claims JWT "curtas" (ex.: "sub") para as URIs longas de
        // ClaimTypes (ex.: ClaimTypes.NameIdentifier) — e UsuarioContextHttp não encontraria "sub".
        bearerOptions.MapInboundClaims = false;

        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Emissor,
            ValidateAudience = true,
            ValidAudience = jwt.Audiencia,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Chave)),
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization();

// --- CORS ---
// Necessário para o visualizador web (Camdas.Web, Blazor WebAssembly) chamar esta Api de outra
// origem (porta diferente) — o app Android não passa pelas regras de CORS do navegador, só quem
// roda em JS/WASM. Libera qualquer origem: a autenticação é por Bearer token (não cookie), então
// não há credenciais implícitas em risco de CSRF.
const string PoliticaCorsWeb = "CamdasWeb";
builder.Services.AddCors(options =>
{
    options.AddPolicy(PoliticaCorsWeb, policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// --- Web API ---
// Enums trafegam como texto no JSON (ex.: "unidade": "Metro"), não como número — mais legível para
// quem consome a Api (inclusive o app Android).
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

// Recusa subir fora de Development com a chave JWT placeholder do appsettings.json versionado no
// repositório — sem isto, um deploy "de produção" esquecido de configurar `Jwt__Chave` (variável de
// ambiente) assinaria tokens com uma chave pública, conhecida por qualquer um que tenha acesso ao
// código-fonte. Ver REVISAO_SEGURANCA.md.
const string ChavePlaceholder = "TROQUE_ESTA_CHAVE_em_producao_min_32_caracteres_1234567890";
if (!app.Environment.IsDevelopment())
{
    var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
    if (jwtOptions.Chave == ChavePlaceholder)
    {
        throw new InvalidOperationException(
            "Jwt:Chave ainda é o valor placeholder do appsettings.json versionado. Configure uma " +
            "chave própria (ex.: variável de ambiente Jwt__Chave) antes de rodar fora de Development.");
    }
}

// Aplica migrations pendentes automaticamente ao iniciar — idempotente (não faz nada se o banco já
// está atualizado). Necessário pra deploy em serviços gerenciados (Render, etc.) onde não há como
// rodar `dotnet ef database update` manualmente antes do primeiro boot; em desenvolvimento local
// continua funcionando igual (só aplica o que ainda não foi aplicado).
using (var escopo = app.Services.CreateScope())
    escopo.ServiceProvider.GetRequiredService<CamdasDbContext>().Database.Migrate();

// Endpoint anônimo e leve, usado apenas pelo app mobile para descobrir em qual rede (casa/trabalho)
// o servidor está acessível antes de tentar autenticar — ver ResolvedorEnderecoApi.
app.MapGet("/health", () => Results.Ok());

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(PoliticaCorsWeb);

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

// Necessário para o WebApplicationFactory<Program> nos testes de integração — Program é `internal`
// por padrão no modelo de hospedagem com top-level statements.
public partial class Program;
