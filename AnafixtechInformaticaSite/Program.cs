// =============================================================================================
// Program.cs — ponto de partida da aplicação.
// Aqui ficam: banco de dados, autenticação (JWT), segurança e o "cano" por onde cada
// requisição passa (middlewares), na ordem em que são executados.
// =============================================================================================
using AnafixtechInformaticaSite.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// 0) Porta do serviço
// Em produção (Render, por exemplo), a plataforma informa a porta na variável PORT e espera
// que a aplicação escute em 0.0.0.0. Em desenvolvimento nada muda: o launchSettings continua
// mandando nas portas 5290/7186.
// ---------------------------------------------------------------------------------------------
var portaPublica = Environment.GetEnvironmentVariable("PORT");
if (int.TryParse(portaPublica, out var porta))
{
    // Endpoint explícito: ignora a porta padrão 8080 que a imagem do .NET define.
    builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(porta));
}

// ---------------------------------------------------------------------------------------------
// 1) MVC (controllers + views)
// ---------------------------------------------------------------------------------------------
builder.Services.AddControllersWithViews();

// Limite do tamanho de upload (foto do equipamento no formulário de solicitação).
// O formulário aceita imagens de até 5 MB; o limite do servidor fica um pouco acima disso.
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 6 * 1024 * 1024; // 6 MB
});

// ---------------------------------------------------------------------------------------------
// 2) Banco de dados
// A string de conexão NÃO fica no código nem no appsettings versionado:
//   - desenvolvimento: user secrets (dotnet user-secrets)
//   - produção: variável de ambiente ConnectionStrings__DefaultConnection
// ---------------------------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "String de conexão ausente. Configure 'ConnectionStrings:DefaultConnection' " +
        "via user secrets (desenvolvimento) ou variável de ambiente ConnectionStrings__DefaultConnection (produção).");
}

// O Render entrega a conexão no formato postgres://usuario:senha@host/banco?sslmode=require.
// O Npgsql só entende o formato chave=valor, então convertemos quando necessário.
connectionString = NormalizarConnectionString(connectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ---------------------------------------------------------------------------------------------
// 3) Autenticação JWT
// A chave de assinatura também é segredo: user secrets / variável Jwt__SecretKey.
// Se ela faltar, a aplicação para na inicialização — é melhor falhar agora do que rodar
// com uma chave fraca ou vazia e permitir que alguém falsifique um token de administrador.
// ---------------------------------------------------------------------------------------------
var jwtSecret = builder.Configuration["Jwt:SecretKey"];

if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "Chave JWT ausente ou curta demais. Configure 'Jwt:SecretKey' (mínimo 32 caracteres) " +
        "via user secrets (desenvolvimento) ou variável de ambiente Jwt__SecretKey (produção).");
}

var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Bearer";
    options.DefaultChallengeScheme = "Bearer";
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;

    // O token chega em um cookie HttpOnly chamado "access_token".
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            ctx.Token = ctx.Request.Cookies["access_token"];
            return Task.CompletedTask;
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        // CORREÇÃO DE SEGURANÇA: antes estava "false", o que desligava a conferência da
        // assinatura do token. Com isso, qualquer pessoa conseguia montar um token dizendo
        // "sou Admin" e entrar na dashboard. Agora a assinatura é conferida de verdade.
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),

        // Não usamos emissor (issuer) nem público (audience) personalizados.
        ValidateIssuer = false,
        ValidateAudience = false,

        // Confere a data de expiração do token (2 horas, definidas no TokenService).
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1) // pequena tolerância de relógio
    };
});

// ---------------------------------------------------------------------------------------------
// 4) Autorização por perfil
// ---------------------------------------------------------------------------------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("User", policy => policy.RequireRole("User"));
});

var app = builder.Build();

// ---------------------------------------------------------------------------------------------
// 4.1) Migrações automáticas (opcional)
// Em produção, a imagem final não tem o SDK nem o dotnet-ef, então não dá para rodar
// "dotnet ef database update" dentro do contêiner. Com Database__MigrateOnStartup=true, a
// aplicação aplica as migrations pendentes ao iniciar. Em desenvolvimento fica desligado:
// use "dotnet ef database update".
// ---------------------------------------------------------------------------------------------
if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var escopo = app.Services.CreateScope();
    var banco = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
    banco.Database.Migrate();
}

// ---------------------------------------------------------------------------------------------
// 5) Cabeçalhos de segurança em TODAS as respostas
//    - X-Content-Type-Options: impede o navegador de "adivinhar" o tipo de um arquivo.
//    - X-Frame-Options: impede que o site seja colocado dentro de um iframe (clickjacking).
//    - Referrer-Policy: não vaza a URL completa para sites externos.
//    - Permissions-Policy: desliga recursos que o site não usa (câmera, microfone...).
// ---------------------------------------------------------------------------------------------
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

    await next();
});

// ---------------------------------------------------------------------------------------------
// 6) Atrás de proxy reverso (Cloudflare Tunnel em casa, Render em produção)
// O proxy entrega a requisição em http, mas avisa no cabeçalho X-Forwarded-Proto que o
// visitante veio por HTTPS. Sem isso, o ASP.NET acharia que o acesso é inseguro e entraria
// em loop de redirecionamento.
//
// - Desenvolvimento: confiamos só no loopback (cloudflared roda na mesma máquina).
// - Produção: o contêiner só recebe tráfego pelo proxy da plataforma, então aceitamos os
//   cabeçalhos de qualquer proxy. Listas vazias = sem restrição de origem.
// ---------------------------------------------------------------------------------------------
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownProxies.Clear();
forwardedOptions.KnownNetworks.Clear();

if (app.Environment.IsDevelopment())
{
    forwardedOptions.KnownProxies.Add(IPAddress.Loopback);     // 127.0.0.1
    forwardedOptions.KnownProxies.Add(IPAddress.IPv6Loopback); // ::1
}

app.UseForwardedHeaders(forwardedOptions);

// ---------------------------------------------------------------------------------------------
// 7) Pipeline HTTP
// ---------------------------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error"); // página amigável de erro
    app.UseHsts();                          // força HTTPS no navegador (produção)
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// ---------------------------------------------------------------------------------------------
// Converte a URL do PostgreSQL (postgres://...) para o formato chave=valor do Npgsql.
// Strings que já estão no formato chave=valor passam sem alteração.
// ---------------------------------------------------------------------------------------------
static string NormalizarConnectionString(string valor)
{
    if (!valor.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !valor.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return valor;
    }

    var uri = new Uri(valor);
    var credenciais = uri.UserInfo.Split(':', 2);

    var cs = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        Username = Uri.UnescapeDataString(credenciais[0]),
        Password = credenciais.Length > 1 ? Uri.UnescapeDataString(credenciais[1]) : null
    };

    // Parâmetros da query (sslmode=require, por exemplo).
    foreach (var par in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var partes = par.Split('=', 2);
        var nome = Uri.UnescapeDataString(partes[0]);
        var conteudo = partes.Length > 1 ? Uri.UnescapeDataString(partes[1]) : string.Empty;

        if (nome.Equals("sslmode", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<SslMode>(conteudo, true, out var modoSsl))
        {
            cs.SslMode = modoSsl;
        }
    }

    return cs.ConnectionString;
}
