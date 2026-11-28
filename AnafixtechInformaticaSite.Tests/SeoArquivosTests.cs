// =============================================================================================
// Testes dos arquivos de SEO gerados pelo SeoController (sem subir o site inteiro).
// Cobrem: permissões do robots.txt, Content-Type, Cache-Control e o conteúdo do sitemap.
// =============================================================================================

using AnafixtechInformaticaSite.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;

namespace AnafixtechInformaticaSite.Tests;

public class SeoArquivosTests
{
    private const string BaseUrlTeste = "https://exemplo.example.test";

    private static SeoController CriarController(
        string baseUrl = BaseUrlTeste,
        string hostDaRequisicao = "host-falso.example")
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Site:BaseUrl"] = baseUrl
            })
            .Build();

        var http = new DefaultHttpContext();
        http.Request.Scheme = "http";
        http.Request.Host = new HostString(hostDaRequisicao);

        return new SeoController(configuracao, new AmbienteFake())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static string ObterRobots(SeoController controller)
    {
        var resultado = Assert.IsType<ContentResult>(controller.Robots());
        return resultado.Content!;
    }

    // ---------------------------------------------------------------------------------------------
    // robots.txt
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RobotsTxt_DeveTerContentTypeETextoCacheaveis()
    {
        var controller = CriarController();
        var resultado = Assert.IsType<ContentResult>(controller.Robots());

        Assert.Equal("text/plain; charset=utf-8", resultado.ContentType);
        Assert.Equal("public, max-age=3600", controller.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData("Googlebot")]
    [InlineData("Googlebot-Smartphone")]
    [InlineData("Bingbot")]
    public void RobotsTxt_DevePermitirPaginasPublicas(string rastreador)
    {
        var robots = ObterRobots(CriarController());

        var paginasPublicas = new[]
        {
            "/",
            "/Services",
            "/Home/Sobre",
            "/Home/Forum",
            "/Home/Privacy",
            "/Home/Terms",
            "/Home/Refund",
            "/sitemap.xml",
            "/robots.txt"
        };

        foreach (var pagina in paginasPublicas)
        {
            Assert.True(
                RobotsParser.PodeRastrear(robots, rastreador, pagina),
                $"{rastreador} deveria poder rastrear {pagina}.");
        }
    }

    [Fact]
    public void RobotsTxt_DeveBloquearAreasPrivadas()
    {
        var robots = ObterRobots(CriarController());

        var areasPrivadas = new[]
        {
            "/Dashboard",
            "/Dashboard/Index",
            "/Account/Login",
            "/Account/Register",
            "/ServiceRequest/Acompanhar",
            "/ServiceRequest/Sucesso"
        };

        foreach (var area in areasPrivadas)
        {
            Assert.False(
                RobotsParser.PodeRastrear(robots, "Googlebot", area),
                $"Googlebot não deveria rastrear {area}.");
        }
    }

    [Fact]
    public void RobotsTxt_NaoDeveConterDisallowDaRaiz()
    {
        // "Disallow: /" é exatamente a resposta que o Render Free devolve quando o serviço
        // está dormindo. Se essa regra aparecer no nosso arquivo, o site inteiro é bloqueado.
        var robots = ObterRobots(CriarController());

        var grupos = RobotsParser.Parse(robots);
        Assert.DoesNotContain(grupos, g => g.Disallow.Contains("/"));
    }

    [Fact]
    public void RobotsTxt_DeveApontarSitemapAbsolutoDaConfiguracao()
    {
        var robots = ObterRobots(CriarController());

        Assert.Contains($"Sitemap: {BaseUrlTeste}/sitemap.xml", robots);
        Assert.DoesNotContain("onrender.com", robots);
        Assert.DoesNotContain("host-falso.example", robots);
    }

    // ---------------------------------------------------------------------------------------------
    // sitemap.xml
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Sitemap_DeveTerContentTypeETextoCacheaveis()
    {
        var controller = CriarController();
        var resultado = Assert.IsType<ContentResult>(controller.Sitemap());

        Assert.Equal("application/xml; charset=utf-8", resultado.ContentType);
        Assert.Equal("public, max-age=3600", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void Sitemap_DeveListarSetePaginasPublicasComLastmod()
    {
        var controller = CriarController();
        var resultado = Assert.IsType<ContentResult>(controller.Sitemap());
        var documento = XDocument.Parse(resultado.Content!);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urls = documento.Root!.Elements(ns + "url").ToList();

        Assert.Equal(7, urls.Count);

        foreach (var url in urls)
        {
            var loc = url.Element(ns + "loc")!.Value;
            var lastmod = url.Element(ns + "lastmod")!.Value;

            Assert.StartsWith(BaseUrlTeste + "/", loc);
            Assert.True(
                DateOnly.TryParseExact(lastmod, "yyyy-MM-dd", out _),
                $"lastmod inválido em {loc}: {lastmod}");
        }
    }

    [Fact]
    public void Sitemap_NaoDeveListarPaginasPrivadasOuDeFormulario()
    {
        var controller = CriarController();
        var resultado = Assert.IsType<ContentResult>(controller.Sitemap());
        var xml = resultado.Content!;

        Assert.DoesNotContain("/ServiceRequest/Create", xml);
        Assert.DoesNotContain("/ServiceRequest/Acompanhar", xml);
        Assert.DoesNotContain("/ServiceRequest/Sucesso", xml);
    }

    [Fact]
    public void Sitemap_DeveIgnorarHostDaRequisicao()
    {
        var controller = CriarController(hostDaRequisicao: "golpe.example");
        var resultado = Assert.IsType<ContentResult>(controller.Sitemap());

        Assert.DoesNotContain("golpe.example", resultado.Content);
        Assert.Contains($"{BaseUrlTeste}/Services", resultado.Content);
    }
}
