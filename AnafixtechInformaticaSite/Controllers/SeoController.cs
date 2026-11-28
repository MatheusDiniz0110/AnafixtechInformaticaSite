// =============================================================================================
// SeoController — serve os dois arquivos que os buscadores leem antes de rastrear o site:
//   /robots.txt  -> diz o que pode e o que não pode ser rastreado;
//   /sitemap.xml -> lista as páginas públicas que devem ser indexadas.
//
// Por que não usar arquivos em wwwroot?
//   O app.UseStaticFiles() roda ANTES do routing. Um arquivo em wwwroot venceria estas actions
//   e passaria a ser a "fonte da verdade" sem ninguém perceber. Foi o que aconteceu com o
//   sitemap.xml estático: ele escondia o sitemap gerado pelo controller. Aqui os dois são
//   gerados pelo próprio site, com Content-Type e Cache-Control explícitos.
//
// Nenhuma das duas actions consulta o banco. Elas respondem mesmo que o PostgreSQL esteja
// indisponível, o que é importante para o Google e para o endpoint /health.
//
// O endereço público vem de "Site:BaseUrl" (appsettings.Production.json ou variável
// Site__BaseUrl no Render), NUNCA do cabeçalho Host da requisição. Assim um Host falso não
// consegue poluir as URLs do sitemap.
// =============================================================================================

using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;

namespace AnafixtechInformaticaSite.Controllers
{
    public class SeoController : Controller
    {
        private readonly IConfiguration _configuracao;
        private readonly IWebHostEnvironment _ambiente;

        public SeoController(IConfiguration configuracao, IWebHostEnvironment ambiente)
        {
            _configuracao = configuracao;
            _ambiente = ambiente;
        }

        // -----------------------------------------------------------------------------------------
        // Páginas públicas e a data da última alteração de cada uma.
        //
        // A data é a do último commit que mexeu na view correspondente (git log -1 --date=short).
        // Ao editar o conteúdo de uma página, atualize a data aqui também: é isso que o Google
        // usa como "lastmod". Não use a data do build, senão toda publicação parece conteúdo novo.
        // -----------------------------------------------------------------------------------------
        private static readonly (string Caminho, DateOnly UltimaAlteracao)[] PaginasPublicas =
        {
            ("/",             new DateOnly(2026, 11, 5)),  // home
            ("/Services",     new DateOnly(2026, 11, 5)),  // catálogo de serviços
            ("/Home/Sobre",   new DateOnly(2026, 11, 5)),  // sobre a empresa
            ("/Home/Forum",   new DateOnly(2026, 11, 5)),  // dúvidas frequentes (FAQPage)
            ("/Home/Privacy", new DateOnly(2026, 11, 24)), // política de privacidade
            ("/Home/Terms",   new DateOnly(2026, 11, 24)), // termos de uso
            ("/Home/Refund",  new DateOnly(2026, 11, 24))  // cancelamento, trocas e reembolso
        };

        // -----------------------------------------------------------------------------------------
        // /robots.txt
        //
        // Regra principal: liberar o site inteiro e bloquear só as áreas privadas reais.
        // /admin/, /api/, /bin/ e /obj/ não existem neste projeto e por isso saíram da lista.
        // Os grupos de rastreadores de IA foram mantidos como estavam (decisão pendente em
        // outra tarefa). A linha Sitemap usa o mesmo endereço público do sitemap gerado.
        // -----------------------------------------------------------------------------------------
        [HttpGet]
        [Route("/robots.txt")]
        public IActionResult Robots()
        {
            var baseUrl = ObterBaseUrl();

            var conteudo = $$"""
                # Rastreadores de busca tradicional
                User-agent: *
                Allow: /

                # Áreas privadas: painel, login, acompanhamento e confirmação de solicitação.
                Disallow: /Dashboard
                Disallow: /Account/
                Disallow: /ServiceRequest/Acompanhar
                Disallow: /ServiceRequest/Sucesso

                # OpenAI
                User-agent: GPTBot
                Allow: /

                User-agent: OAI-SearchBot
                Allow: /

                User-agent: ChatGPT-User
                Allow: /

                # Anthropic
                User-agent: ClaudeBot
                Allow: /

                User-agent: Claude-SearchBot
                Allow: /

                User-agent: Claude-User
                Allow: /

                # Perplexity
                User-agent: PerplexityBot
                Allow: /

                User-agent: Perplexity-User
                Allow: /

                # Google (IA e treinamento)
                User-agent: Google-Extended
                Allow: /

                # Apple
                User-agent: Applebot-Extended
                Allow: /

                # Outros rastreadores de IA
                User-agent: CCBot
                Allow: /

                User-agent: Amazonbot
                Allow: /

                User-agent: meta-externalagent
                Allow: /

                User-agent: cohere-ai
                Allow: /

                Sitemap: {{baseUrl}}/sitemap.xml
                """;

            // text/plain com charset: é o formato que o Google espera para robots.txt.
            // max-age=3600 evita que o arquivo seja buscado a cada requisição, mas ainda
            // permite corrigir uma regra em até 1 hora.
            Response.Headers.CacheControl = "public, max-age=3600";
            return Content(conteudo, "text/plain; charset=utf-8");
        }

        // -----------------------------------------------------------------------------------------
        // /sitemap.xml
        // -----------------------------------------------------------------------------------------
        [HttpGet]
        [Route("/sitemap.xml")]
        public IActionResult Sitemap()
        {
            var baseUrl = ObterBaseUrl();
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

            var documento = new XDocument(
                new XElement(ns + "urlset",
                    PaginasPublicas.Select(pagina =>
                        new XElement(ns + "url",
                            new XElement(ns + "loc", baseUrl + pagina.Caminho),
                            new XElement(ns + "lastmod", pagina.UltimaAlteracao.ToString("yyyy-MM-dd"))
                        )
                    )
                )
            );

            // Declaração XML no topo, como os buscadores esperam.
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + documento.ToString();

            Response.Headers.CacheControl = "public, max-age=3600";
            return Content(xml, "application/xml; charset=utf-8");
        }

        // -----------------------------------------------------------------------------------------
        // Endereço público canônico (sem barra no final).
        //
        // Em produção é obrigatório ter Site:BaseUrl configurado. Em desenvolvimento, se ninguém
        // configurou, usamos o Host local apenas para facilitar o trabalho — nunca em produção.
        // -----------------------------------------------------------------------------------------
        private string ObterBaseUrl()
        {
            var configurado = _configuracao["Site:BaseUrl"] ?? _configuracao["SiteUrl"];

            if (string.IsNullOrWhiteSpace(configurado))
            {
                if (!_ambiente.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "Site:BaseUrl não configurado. Defina Site__BaseUrl com o endereço público " +
                        "do site (sem barra final) e faça um novo deploy.");
                }

                return $"{Request.Scheme}://{Request.Host}";
            }

            var texto = configurado.Trim().TrimEnd('/');

            if (!Uri.TryCreate(texto, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                string.IsNullOrWhiteSpace(uri.Host))
            {
                // Nenhum valor é impresso: a mensagem só aponta a variável errada.
                throw new InvalidOperationException(
                    "Site:BaseUrl inválido. Use uma URL absoluta http:// ou https:// " +
                    "(variável de ambiente Site__BaseUrl).");
            }

            return texto;
        }
    }
}
