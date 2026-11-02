using System.Globalization; // CultureInfo: define o "jeito brasileiro" de formatar números e datas

namespace AnafixtechInformaticaSite.Models
{
    // Funções prontas para mostrar dinheiro e datas sempre no padrão brasileiro nas views.
    // Uso: @Fmt.Money(pedido.FinalPrice)  →  "R$ 120,00"
    public static class Fmt
    {
        // Cultura pt-BR criada uma única vez e reaproveitada por todos os métodos.
        private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

        // Dinheiro a partir de um decimal que sempre existe.
        public static string Money(decimal value) => value.ToString("C", Pt);

        // Dinheiro a partir de um decimal que pode ser nulo; nulo vira um traço.
        public static string Money(decimal? value) => value.HasValue ? value.Value.ToString("C", Pt) : "—";

        // Data (sem hora) a partir de um DateTime que pode ser nulo.
        public static string Date(DateTime? value) => value.HasValue ? value.Value.ToString("dd/MM/yyyy", Pt) : "—";

        // Data a partir de um DateOnly que pode ser nulo.
        public static string Date(DateOnly? value) => value.HasValue ? value.Value.ToString("dd/MM/yyyy", Pt) : "—";

        // Data e hora juntas (ex.: 31/10/2026 14:30).
        public static string Stamp(DateTime? value) => value.HasValue ? value.Value.ToString("dd/MM/yyyy HH:mm", Pt) : "—";

        // Só a hora (ex.: 14:30).
        public static string Time(DateTime value) => value.ToString("HH:mm", Pt);
    }
}
