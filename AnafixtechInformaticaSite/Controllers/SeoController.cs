// =============================================================================================
// SeoController — serve dois arquivos que TODO site público deveria ter:
//
//   /robots.txt    → diz aos buscadores o que podem ou não visitar
//   /sitemap.xml   → lista as páginas que devem ser indexadas
//
// Eles são gerados pelo próprio site (e não arquivos fixos) porque o endereço do domínio
// só é conhecido em tempo de execução. Assim funciona igual em localhost, no domínio atual
// e em qualquer domínio futuro, sem precisar editar nada.
// =============================================================================================

using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml.Linq;

namespace AnafixtechInformaticaSite.Controllers
{
    public class SeoController : Controller
    {
        // Monta a base do site (ex.: https://anafixtech.com.br) a partir da requisição.
        private string UrlBase => $"{Request.Scheme}://{Request.Host}";

        // -----------------------------------------------------------------------------------------
        // /robots.txt
        // -----------------------------------------------------------------------------------------
        [HttpGet]
        [Route("/robots.txt")]
        [ResponseCache(Duration = 3600)] // pode ficar em cache por 1 hora
        public IActionResult Robots()
        {
            var texto = new StringBuilder()
                .AppendLine("User-agent: *")
                .AppendLine("Allow: /")
                // Áreas que não fazem sentido no Google e não devem ser rastreadas:
                .AppendLine("Disallow: /Dashboard")
                .AppendLine("Disallow: /Account")
                .AppendLine()
                .AppendLine($"Sitemap: {UrlBase}/sitemap.xml")
                .ToString();

            return Content(texto, "text/plain; charset=utf-8");
        }

        // -----------------------------------------------------------------------------------------
        // /sitemap.xml
        // -----------------------------------------------------------------------------------------
        [HttpGet]
        [Route("/sitemap.xml")]
        [ResponseCache(Duration = 3600)]
        public IActionResult Sitemap()
        {
            // Páginas públicas do site, na ordem de importância.
            var caminhos = new[]
            {
                "/",                       // home
                "/Services",               // catálogo de serviços
                "/ServiceRequest/Create",  // formulário de solicitação
                "/Home/Sobre",             // sobre a empresa
                "/Home/Forum",             // dúvidas frequentes
                "/ServiceRequest/Acompanhar", // consulta de protocolo
                "/Home/Privacy"            // política de privacidade
            };

            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

            var documento = new XDocument(
                new XElement(ns + "urlset",
                    caminhos.Select(caminho =>
                        new XElement(ns + "url",
                            new XElement(ns + "loc", UrlBase + caminho)
                        )
                    )
                )
            );

            // Declaração XML no topo, como os buscadores esperam.
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + documento.ToString();

            return Content(xml, "application/xml; charset=utf-8");
        }
    }
}
