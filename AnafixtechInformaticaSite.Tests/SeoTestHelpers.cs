// =============================================================================================
// Ferramentas compartilhadas dos testes de SEO.
//
// O parser abaixo é uma versão reduzida da regra do Google para robots.txt:
//   - o grupo escolhido é o do user-agent mais específico que casa com o rastreador;
//   - se não houver grupo específico, vale o grupo "*";
//   - a regra mais longa vence; em empate, Allow vence Disallow.
//
// Ele existe para provar que o robots.txt servido pelo site não bloqueia as páginas públicas,
// e não para substituir o teste do Search Console.
// =============================================================================================

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace AnafixtechInformaticaSite.Tests;

internal static class RobotsParser
{
    internal sealed record Grupo(List<string> Agentes, List<string> Allow, List<string> Disallow);

    internal static List<Grupo> Parse(string texto)
    {
        var grupos = new List<Grupo>();
        Grupo? atual = null;
        var ultimaLinhaFoiAgente = false;

        foreach (var linhaCrua in texto.Split('\n'))
        {
            var linha = linhaCrua;

            // Tudo depois de "#" é comentário.
            var cerquilha = linha.IndexOf('#');
            if (cerquilha >= 0) linha = linha[..cerquilha];

            linha = linha.Trim();
            if (linha.Length == 0) continue;

            var doisPontos = linha.IndexOf(':');
            if (doisPontos < 0) continue;

            var campo = linha[..doisPontos].Trim().ToLowerInvariant();
            var valor = linha[(doisPontos + 1)..].Trim();

            if (campo == "user-agent")
            {
                // Várias linhas "User-agent:" seguidas compartilham o mesmo grupo de regras.
                if (atual is null || !ultimaLinhaFoiAgente)
                {
                    atual = new Grupo(new List<string>(), new List<string>(), new List<string>());
                    grupos.Add(atual);
                }

                atual.Agentes.Add(valor.ToLowerInvariant());
                ultimaLinhaFoiAgente = true;
            }
            else if (campo is "allow" or "disallow")
            {
                if (atual is null || valor.Length == 0) continue;

                if (campo == "allow") atual.Allow.Add(valor);
                else atual.Disallow.Add(valor);

                ultimaLinhaFoiAgente = false;
            }
        }

        return grupos;
    }

    internal static bool PodeRastrear(string robotsTxt, string rastreador, string caminho)
    {
        var grupos = Parse(robotsTxt);

        var especificos = grupos
            .SelectMany(g => g.Agentes.Select(a => (Agente: a, Grupo: g)))
            .Where(x => x.Agente != "*" &&
                        rastreador.StartsWith(x.Agente, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Agente.Length)
            .ToList();

        List<Grupo> aplicaveis;

        if (especificos.Count > 0)
        {
            var maisEspecifico = especificos[0].Agente;
            aplicaveis = especificos
                .Where(x => x.Agente == maisEspecifico)
                .Select(x => x.Grupo)
                .ToList();
        }
        else
        {
            aplicaveis = grupos.Where(g => g.Agentes.Contains("*")).ToList();
        }

        var tamanhoVencedor = -1;
        var vencedorEhAllow = true;
        var temRegra = false;

        foreach (var grupo in aplicaveis)
        {
            var regras = grupo.Allow.Select(r => (Caminho: r, Allow: true))
                .Concat(grupo.Disallow.Select(r => (Caminho: r, Allow: false)));

            foreach (var regra in regras)
            {
                if (!caminho.StartsWith(regra.Caminho, StringComparison.Ordinal)) continue;

                // Regra mais longa vence. No empate, Allow vence Disallow.
                if (regra.Caminho.Length > tamanhoVencedor ||
                    (regra.Caminho.Length == tamanhoVencedor && regra.Allow && !vencedorEhAllow))
                {
                    tamanhoVencedor = regra.Caminho.Length;
                    vencedorEhAllow = regra.Allow;
                    temRegra = true;
                }
            }
        }

        // Sem nenhuma regra que case, o padrão é permitir.
        return !temRegra || vencedorEhAllow;
    }
}

// Ambiente falso para instanciar o SeoController sem subir o site inteiro.
internal sealed class AmbienteFake : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "testes";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Production";
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
