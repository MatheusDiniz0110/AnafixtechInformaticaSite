using System.Globalization; // CultureInfo e NumberStyles, usados na conversão do texto em número

namespace AnafixtechInformaticaSite.Services
{
    // Converte o que o usuário digitou ("120,50", "120.50", "1.200,00", "R$ 1200") em decimal.
    // Evita o erro clássico: no Brasil "120.50" seria lido como 12050 (o ponto separa milhares).
    public static class MoneyParser
    {
        // Tenta converter. Devolve true e preenche "value" se der certo; false se o texto for inválido.
        public static bool TryParse(string? input, out decimal value)
        {
            value = 0; // valor padrão caso a conversão falhe
            if (string.IsNullOrWhiteSpace(input)) return false; // texto vazio não é um valor

            // Remove "R$" e espaços para sobrar só números e separadores.
            var s = input.Trim().Replace("R$", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "");

            var lastComma = s.LastIndexOf(','); // posição da última vírgula (-1 se não houver)
            var lastDot = s.LastIndexOf('.');   // posição do último ponto (-1 se não houver)

            if (lastComma >= 0 && lastDot >= 0)
            {
                // Tem vírgula E ponto: o que vier por último é o separador decimal.
                s = lastComma > lastDot
                    ? s.Replace(".", "").Replace(',', '.')  // "1.200,50": tira os pontos, vírgula vira ponto
                    : s.Replace(",", "");                   // "1,200.50": só tira as vírgulas
            }
            else if (lastComma >= 0)
            {
                s = s.Replace(',', '.'); // só vírgula: ela é o separador decimal ("120,50" → "120.50")
            }
            else if (lastDot >= 0)
            {
                // Só ponto: é decimal se houver um único ponto seguido de 1 ou 2 dígitos ("120.5", "120.50").
                var single = s.IndexOf('.') == lastDot;      // existe apenas um ponto?
                var digitsAfter = s.Length - lastDot - 1;    // quantos dígitos vêm depois dele
                if (!(single && digitsAfter is >= 1 and <= 2))
                    s = s.Replace(".", ""); // senão o ponto era separador de milhar ("1.200" → "1200")
            }

            // Converte usando a cultura invariante (ponto decimal) e rejeita números negativos.
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0;
        }
    }
}
