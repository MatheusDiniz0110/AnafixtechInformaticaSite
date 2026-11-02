namespace AnafixtechInformaticaSite.Services
{
    // Entrega a data/hora de Brasília, não importa o fuso do servidor.
    // Servidores e containers Docker costumam rodar em UTC; sem isto, "hoje" viraria "amanhã" à noite.
    public static class Clock
    {
        // Fuso horário de Brasília, descoberto uma única vez quando a classe é carregada.
        private static readonly TimeZoneInfo Zone = Resolve();

        // Data e hora atuais no horário de Brasília.
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

        // Somente a data de hoje no horário de Brasília.
        public static DateOnly Today => DateOnly.FromDateTime(Now);

        // Procura o fuso pelo nome do Linux/Docker e, se não achar, pelo nome do Windows.
        private static TimeZoneInfo Resolve()
        {
            foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); } // achou: usa este fuso
                catch (TimeZoneNotFoundException) { }                    // não existe neste sistema: tenta o próximo
                catch (InvalidTimeZoneException) { }                     // dados do fuso inválidos: tenta o próximo
            }
            return TimeZoneInfo.Local; // último recurso: o fuso do servidor
        }
    }
}
