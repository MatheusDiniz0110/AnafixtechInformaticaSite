// =============================================================================================
// SeoController — serve o /sitemap.xml, que lista as páginas que devem ser indexadas.
//
// O sitemap é gerado pelo próprio site (e não é arquivo fixo) porque o endereço do domínio
// só é conhecido em tempo de execução. Assim funciona igual em localhost, no domínio atual
// e em qualquer domínio futuro, sem precisar editar nada.
//
// O /robots.txt agora é um arquivo estático em wwwroot/robots.txt, servido pelo
// app.UseStaticFiles() antes do routing; não deve haver action que capture essa rota.
// =============================================================================================

using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;

namespace AnafixtechInformaticaSite.Controllers
{
    public class SeoController : Controller
    {
        // Monta a base do site (ex.: https://anafixtech.com.br) a partir da requisição.
        private string UrlBase => $"{Request.Scheme}://{Request.Host}";

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
                "/Home/Privacy",           // política de privacidade
                "/Home/Terms",             // termos de uso
                "/Home/Refund"             // cancelamento, trocas e reembolso
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
