// Este arquivo reúne todos os "enums" (listas fixas de opções) usados pela dashboard.
// Um enum é um tipo que só aceita um dos valores listados — evita erros de digitação com textos soltos.
namespace AnafixtechInformaticaSite.Models
{
    // Situação de uma Ordem de Serviço (OS). Espelha o campo "status" do ServiceOrder do Base44.
    public enum OrderStatus
    {
        Pending,    // Novo: o pedido acabou de chegar
        Waiting,    // Aguardando: peça, resposta do cliente ou aprovação de orçamento
        InProgress, // Em andamento: o técnico está trabalhando nele
        Completed,  // Concluído: o serviço está pronto
        Delivered,  // Entregue ao cliente (etapa final normal)
        Cancelled   // Cancelado de fato
    }

    // Nível de urgência do pedido (campo "urgency" do Base44).
    public enum UrgencyLevel
    {
        Normal,  // Prazo normal
        Urgent,  // Urgente
        Future   // Pode ser feito depois
    }

    // Situação do pagamento de uma OS (campo "payment_status" do Base44).
    public enum OrderPaymentStatus
    {
        Pending, // Nada recebido ou recebido só em parte
        Paid,    // Totalmente pago
        Expired  // Cobrança expirada
    }

    // Formas de pagamento aceitas (campo "forma_pagamento" do FinancialEntry no Base44).
    public enum PaymentMethod
    {
        Pix,
        Dinheiro,
        CartaoDebito,
        CartaoCredito,
        Transferencia,
        Boleto
    }

    // Tipo de um lançamento financeiro.
    public enum EntryType
    {
        Entrada, // Dinheiro que entrou
        Saida    // Dinheiro que saiu
    }

    // De onde veio o lançamento financeiro (campo "origem" no Base44).
    public enum EntryOrigin
    {
        Manual,  // Digitado na tela de financeiro
        OS,      // Gerado automaticamente por um pagamento de OS
        Estoque  // Gerado automaticamente por uma compra de peça
    }

    // Tipo de compromisso na agenda.
    public enum AgendamentoTipo
    {
        OS,      // Ligado a uma ordem de serviço
        Visita,  // Visita técnica ao cliente
        Outro    // Qualquer outro compromisso
    }

    // Situação de um compromisso da agenda.
    public enum AgendamentoStatus
    {
        Agendado,
        Concluido,
        Cancelado
    }

    // Situação de um horário disponível para atendimento.
    public enum SlotStatus
    {
        Disponivel, // Livre para o cliente escolher
        Reservado,  // Já tem cliente
        Bloqueado   // Indisponível (folga, compromisso externo...)
    }

    // Prioridade de um ticket de suporte.
    public enum TicketPriority
    {
        Low,    // Baixa
        Medium, // Média
        High    // Alta
    }

    // Situação de um ticket de suporte.
    public enum TicketStatus
    {
        Pending,    // Aguardando atendimento
        InProgress, // Em atendimento
        Resolved    // Resolvido
    }

    // Categoria de uma notificação do painel (campo "type" no Base44).
    public enum NotificationType
    {
        OsNew,   // Nova OS
        Payment, // Pagamento recebido
        Stock,   // Estoque baixo
        Ticket,  // Novo ticket
        Urgent,  // Algo urgente
        Info     // Informação geral
    }

    // Nível do estoque de uma peça. NÃO é gravado no banco: é calculado a partir das quantidades.
    public enum StockLevel
    {
        Ok,       // Acima do mínimo
        Low,      // No mínimo ou abaixo
        Critical  // Acabou (quantidade zero)
    }

    // Métodos de extensão: adicionam "ToLabel()" (texto em português) e "ToBadge()" (cor do selo)
    // a cada enum. Exemplo de uso numa view: @pedido.Status.ToLabel()
    public static class EnumLabels
    {
        // Texto em português do status da OS.
        public static string ToLabel(this OrderStatus v) => v switch
        {
            OrderStatus.Pending => "Novo",
            OrderStatus.Waiting => "Aguardando",
            OrderStatus.InProgress => "Em andamento",
            OrderStatus.Completed => "Concluído",
            OrderStatus.Delivered => "Entregue",
            OrderStatus.Cancelled => "Cancelado",
            _ => v.ToString() // qualquer valor não previsto mostra o nome do enum
        };

        // Cor do selo (classe CSS "db-badge--cor") do status da OS.
        public static string ToBadge(this OrderStatus v) => v switch
        {
            OrderStatus.Pending => "amber",
            OrderStatus.Waiting => "purple",
            OrderStatus.InProgress => "blue",
            OrderStatus.Completed => "cyan",
            OrderStatus.Delivered => "green",
            OrderStatus.Cancelled => "gray",
            _ => "gray"
        };

        // Texto da urgência.
        public static string ToLabel(this UrgencyLevel v) => v switch
        {
            UrgencyLevel.Urgent => "Urgente",
            UrgencyLevel.Future => "Pode esperar",
            _ => "Normal"
        };

        // Cor do selo da urgência.
        public static string ToBadge(this UrgencyLevel v) => v switch
        {
            UrgencyLevel.Urgent => "red",
            UrgencyLevel.Future => "gray",
            _ => "blue"
        };

        // Texto da situação do pagamento.
        public static string ToLabel(this OrderPaymentStatus v) => v switch
        {
            OrderPaymentStatus.Paid => "Pago",
            OrderPaymentStatus.Expired => "Expirado",
            _ => "Pendente"
        };

        // Cor do selo da situação do pagamento.
        public static string ToBadge(this OrderPaymentStatus v) => v switch
        {
            OrderPaymentStatus.Paid => "green",
            OrderPaymentStatus.Expired => "red",
            _ => "amber"
        };

        // Texto da forma de pagamento.
        public static string ToLabel(this PaymentMethod v) => v switch
        {
            PaymentMethod.Pix => "Pix",
            PaymentMethod.Dinheiro => "Dinheiro",
            PaymentMethod.CartaoDebito => "Cartão de débito",
            PaymentMethod.CartaoCredito => "Cartão de crédito",
            PaymentMethod.Transferencia => "Transferência",
            PaymentMethod.Boleto => "Boleto",
            _ => v.ToString()
        };

        // Texto do tipo de lançamento.
        public static string ToLabel(this EntryType v) => v == EntryType.Entrada ? "Entrada" : "Saída";

        // Texto da origem do lançamento.
        public static string ToLabel(this EntryOrigin v) => v switch
        {
            EntryOrigin.OS => "OS",
            EntryOrigin.Estoque => "Estoque",
            _ => "Manual"
        };

        // Texto do tipo de agendamento.
        public static string ToLabel(this AgendamentoTipo v) => v switch
        {
            AgendamentoTipo.OS => "OS",
            AgendamentoTipo.Visita => "Visita",
            _ => "Outro"
        };

        // Texto da situação do agendamento.
        public static string ToLabel(this AgendamentoStatus v) => v switch
        {
            AgendamentoStatus.Concluido => "Concluído",
            AgendamentoStatus.Cancelado => "Cancelado",
            _ => "Agendado"
        };

        // Cor do selo da situação do agendamento.
        public static string ToBadge(this AgendamentoStatus v) => v switch
        {
            AgendamentoStatus.Concluido => "green",
            AgendamentoStatus.Cancelado => "gray",
            _ => "blue"
        };

        // Texto da situação do horário.
        public static string ToLabel(this SlotStatus v) => v switch
        {
            SlotStatus.Reservado => "Reservado",
            SlotStatus.Bloqueado => "Bloqueado",
            _ => "Disponível"
        };

        // Cor do selo da situação do horário.
        public static string ToBadge(this SlotStatus v) => v switch
        {
            SlotStatus.Reservado => "blue",
            SlotStatus.Bloqueado => "gray",
            _ => "green"
        };

        // Texto da prioridade do ticket.
        public static string ToLabel(this TicketPriority v) => v switch
        {
            TicketPriority.High => "Alta",
            TicketPriority.Low => "Baixa",
            _ => "Média"
        };

        // Cor do selo da prioridade do ticket.
        public static string ToBadge(this TicketPriority v) => v switch
        {
            TicketPriority.High => "red",
            TicketPriority.Low => "gray",
            _ => "amber"
        };

        // Texto da situação do ticket.
        public static string ToLabel(this TicketStatus v) => v switch
        {
            TicketStatus.InProgress => "Em atendimento",
            TicketStatus.Resolved => "Resolvido",
            _ => "Pendente"
        };

        // Cor do selo da situação do ticket.
        public static string ToBadge(this TicketStatus v) => v switch
        {
            TicketStatus.InProgress => "blue",
            TicketStatus.Resolved => "green",
            _ => "amber"
        };

        // Texto do nível de estoque.
        public static string ToLabel(this StockLevel v) => v switch
        {
            StockLevel.Critical => "Crítico",
            StockLevel.Low => "Baixo",
            _ => "OK"
        };

        // Cor do selo do nível de estoque.
        public static string ToBadge(this StockLevel v) => v switch
        {
            StockLevel.Critical => "red",
            StockLevel.Low => "amber",
            _ => "green"
        };
    }
}
