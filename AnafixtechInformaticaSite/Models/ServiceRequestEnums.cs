namespace AnafixtechInformaticaSite.Models
{
    // Situação de uma solicitação de serviço (formulário público).
    // É diferente do status da OS: aqui acompanhamos o pedido ANTES de ele virar OS.
    public enum RequestStatus
    {
        Pending,            // Pendente: acabou de chegar, ninguém analisou ainda
        InReview,           // Em análise: o técnico está avaliando o caso
        WaitingForApproval, // Aguardando aprovação do orçamento pelo cliente
        WaitingForParts,    // Aguardando peças chegarem
        InProgress,         // Em andamento: serviço sendo executado
        WaitingForPayment,  // Aguardando pagamento
        WaitingForPickUp,   // Pronta para retirada
        Completed,          // Concluída
        Converted,          // Convertida em Ordem de Serviço
        Cancelled           // Cancelada
    }

    // Textos e cores usados nas telas de solicitação.
    // Ficam num arquivo separado para não alterar o DashboardEnums.cs que já existe.
    public static class RequestStatusLabels
    {
        // Texto em português da situação.
        public static string ToLabel(this RequestStatus v) => v switch
        {
            RequestStatus.Pending => "Pendente",
            RequestStatus.InReview => "Em análise",
            RequestStatus.WaitingForApproval => "Aguardando aprovação",
            RequestStatus.WaitingForParts => "Aguardando peças",
            RequestStatus.InProgress => "Em andamento",
            RequestStatus.WaitingForPayment => "Aguardando pagamento",
            RequestStatus.WaitingForPickUp => "Pronta para retirada",
            RequestStatus.Completed => "Concluída",
            RequestStatus.Converted => "Convertida em OS",
            RequestStatus.Cancelled => "Cancelada",
            _ => v.ToString()
        };

        // Cor do selo (classe CSS "db-badge--cor").
        public static string ToBadge(this RequestStatus v) => v switch
        {
            RequestStatus.Pending => "amber",
            RequestStatus.InReview => "purple",
            RequestStatus.WaitingForApproval => "purple",
            RequestStatus.WaitingForParts => "amber",
            RequestStatus.InProgress => "blue",
            RequestStatus.WaitingForPayment => "amber",
            RequestStatus.WaitingForPickUp => "cyan",
            RequestStatus.Completed => "green",
            RequestStatus.Converted => "green",
            RequestStatus.Cancelled => "gray",
            _ => "gray"
        };
    }
}
