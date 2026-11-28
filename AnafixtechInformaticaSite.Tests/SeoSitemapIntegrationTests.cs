// =============================================================================================
// Teste de integração do SEO: sobe o site em memória e confere o que o Google realmente vê.
//
// Para cada URL do sitemap, exige:
//   - status 200 (sem redirecionamento);
//   - ausência de <meta name="robots" content="noindex...">;
//   - <link rel="canonical"> igual à URL publicada no sitemap.
//
// Também confere Content-Type de /robots.txt e /sitemap.xml, o endpoint /health e o arquivo
// de verificação do Google.
// =============================================================================================

using AnafixtechInformaticaSite.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Xml.Linq;

namespace AnafixtechInformaticaSite.Tests;

// Fábrica usada por todos os testes desta classe. O banco é em memória: nenhum dado real
// é lido ou gravado.
public sealed class FabricaSiteTeste : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Development" evita as exigências de produção (segredos, Site:BaseUrl obrigatório)
        // e faz os erros aparecerem no teste em vez de virarem uma página genérica.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuracao) =>
        {
            configuracao.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste",
                ["Jwt:SecretKey"] = new string('t', 40),
                ["Database:MigrateOnStartup"] = "false",
                ["Site:BaseUrl"] = "http://localhost"
            });
        });

        builder.ConfigureServices(servicos =>
        {
            // Troca o PostgreSQL pelo banco em memória. As migrations não rodam.
            // Além do DbContext, é preciso remover o provedor Npgsql já registrado: o EF Core
            // não aceita dois provedores (Npgsql + InMemory) no mesmo service provider.
            servicos.RemoveAll<Microsoft.EntityFrameworkCore.Storage.IDatabaseProvider>();
            servicos.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>>();
            servicos.RemoveAll<DbContextOptions<AppDbContext>>();
            servicos.RemoveAll<AppDbContext>();
            servicos.AddDbContext<AppDbContext>(opcoes =>
                opcoes.UseInMemoryDatabase("seo-integracao"));
        });
    }
}

public class SeoSitemapIntegrationTests : IClassFixture<FabricaSiteTeste>
{
    private readonly FabricaSiteTeste _fabrica;

    public SeoSitemapIntegrationTests(FabricaSiteTeste fabrica) => _fabrica = fabrica;

    private HttpClient CriarCliente() => _fabrica.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("http://localhost")
    });

    [Fact]
    public async Task Sitemap_TodasAsUrlsDevemSerIndexaveis()
    {
        using var cliente = CriarCliente();

        var respostaSitemap = await cliente.GetAsync("/sitemap.xml");
        Assert.Equal(HttpStatusCode.OK, respostaSitemap.StatusCode);
        Assert.StartsWith("application/xml", respostaSitemap.Content.Headers.ContentType!.ToString());

        var xml = await respostaSitemap.Content.ReadAsStringAsync();
        var documento = XDocument.Parse(xml);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var enderecos = documento.Root!
            .Elements(ns + "url")
            .Select(u => u.Element(ns + "loc")!.Value)
            .ToList();

        Assert.Equal(7, enderecos.Count);

        foreach (var endereco in enderecos)
        {
            var caminho = new Uri(endereco).PathAndQuery;
            var resposta = await cliente.GetAsync(caminho);

            Assert.True(
                resposta.StatusCode == HttpStatusCode.OK,
                $"{caminho} deveria responder 200, mas respondeu {(int)resposta.StatusCode}.");

            var html = await resposta.Content.ReadAsStringAsync();

            Assert.DoesNotContain("content=\"noindex", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"<link rel=\"canonical\" href=\"{endereco}\" />", html);
        }
    }

    [Fact]
    public async Task RobotsTxt_DeveSerServidoComContentTypeCorreto()
    {
        using var cliente = CriarCliente();
        var resposta = await cliente.GetAsync("/robots.txt");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("text/plain", resposta.Content.Headers.ContentType!.MediaType);

        var robots = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("Sitemap: http://localhost/sitemap.xml", robots);
        Assert.DoesNotContain("Disallow: /\n", robots.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task Health_DeveResponderSemConsultarOBanco()
    {
        using var cliente = CriarCliente();
        var resposta = await cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("ok", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ArquivoDeVerificacaoDoGoogle_DeveContinuarAcessivel()
    {
        using var cliente = CriarCliente();
        var resposta = await cliente.GetAsync("/google21f2cd70c10954c2.html");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var conteudo = await resposta.Content.ReadAsStringAsync();
        Assert.StartsWith("google-site-verification:", conteudo);
    }
}
