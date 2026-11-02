using System.ComponentModel.DataAnnotations;        // atributos de validação: [Key], [Required], [StringLength]
using System.ComponentModel.DataAnnotations.Schema; // atributos de banco: [Table], [NotMapped]

namespace AnafixtechInformaticaSite.Models
{
    // Ordem de Serviço (OS): o pedido de reparo/serviço de um cliente.
    // Corresponde à entidade "ServiceOrder" do Base44.
    [Table("ServiceOrders")] // nome da tabela criada no SQL Server
    public class ServiceOrder
    {
        [Key] // chave primária; o banco gera o número sozinho (1, 2, 3...)
        public int Id { get; set; }

        // Código amigável mostrado na tela (ex.: OS-00012). Não é coluna do banco.
        [NotMapped]
        public string Codigo => $"OS-{Id:D5}";

        // ---------- Dados do cliente ----------
        [Required, StringLength(120)] // obrigatório, até 120 caracteres
        public string ClientName { get; set; } = string.Empty; // nome do cliente

        [Required, EmailAddress, StringLength(160)] // obrigatório e precisa ter formato de e-mail
        public string ClientEmail { get; set; } = string.Empty; // e-mail do cliente

        [Required, StringLength(30)]
        public string ClientPhone { get; set; } = string.Empty; // telefone/WhatsApp do cliente

        [StringLength(250)]
        public string? ClientAddress { get; set; } // endereço (opcional; usado para busca/entrega)

        public bool PickupRequested { get; set; } // true = cliente quer que busquemos o equipamento

        // ---------- Equipamento e problema ----------
        [Required, StringLength(120)]
        public string ProductName { get; set; } = string.Empty; // nome do equipamento (ex.: Notebook Dell)

        [Required, StringLength(2000)]
        public string ProblemDescription { get; set; } = string.Empty; // problema relatado pelo cliente

        public UrgencyLevel Urgency { get; set; } = UrgencyLevel.Normal; // urgência do pedido

        // ---------- Andamento ----------
        public OrderStatus Status { get; set; } = OrderStatus.Pending; // situação atual da OS

        [StringLength(2000)]
        public string? TechnicianNotes { get; set; } // anotações internas do técnico

        public DateOnly? DeliveryDate { get; set; } // data prevista de entrega

        // ---------- Valores e pagamento ----------
        public decimal? EstimatedPrice { get; set; } // preço estimado (nulo = "a combinar")

        [StringLength(120)]
        public string? PriceNote { get; set; } // observação quando não há preço (ex.: "A combinar")

        public decimal? FinalPrice { get; set; } // preço final combinado

        public PaymentMethod? PaymentMethod { get; set; } // forma do último pagamento (nulo = nenhum ainda)

        public OrderPaymentStatus PaymentStatus { get; set; } = OrderPaymentStatus.Pending; // situação do pagamento

        public DateOnly? PaymentDate { get; set; } // data do último pagamento

        public decimal ValorRecebido { get; set; } // soma de tudo que já foi recebido nesta OS

        // ---------- Relacionamentos ----------
        public int? ServiceId { get; set; } // chave estrangeira: qual serviço do catálogo (opcional)
        public Service? Service { get; set; } // o serviço em si (o EF preenche com Include)

        public int? StockItemId { get; set; } // chave estrangeira: peça do estoque usada (opcional)
        public StockItem? StockItem { get; set; } // a peça em si

        // ---------- Controle ----------
        public DateTime CreatedAt { get; set; } // quando a OS foi criada
        public DateTime UpdatedAt { get; set; } // última alteração

        public List<StatusHistoryEntry> StatusHistory { get; set; } = new(); // histórico de mudanças de status
        public List<FinancialEntry> FinancialEntries { get; set; } = new();  // lançamentos financeiros desta OS
    }
}
