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
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

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
// 6) Atrás do Cloudflare Tunnel (servidor de casa)
// O cloudflared entrega a requisição em http://localhost, mas avisa no cabeçalho
// X-Forwarded-Proto que o visitante veio por HTTPS. Sem isso, o ASP.NET acharia que o
// acesso é inseguro e poderia entrar em loop de redirecionamento.
// Só confiamos nesse cabeçalho quando ele vem da própria máquina (loopback).
// ---------------------------------------------------------------------------------------------
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownProxies.Clear();
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Add(IPAddress.Loopback);   // 127.0.0.1
forwardedOptions.KnownProxies.Add(IPAddress.IPv6Loopback); // ::1
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
