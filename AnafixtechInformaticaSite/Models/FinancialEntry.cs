using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnafixtechInformaticaSite.Models
{
    // Lançamento financeiro (entrada ou saída de dinheiro).
    // Corresponde à entidade "FinancialEntry" do Base44 — é a ÚNICA tabela financeira do sistema.
    [Table("FinancialEntries")]
    public class FinancialEntry
    {
        [Key]
        public int Id { get; set; } // chave primária

        public DateOnly Data { get; set; } // dia do lançamento

        public EntryType Tipo { get; set; } // Entrada ou Saída

        [Required, StringLength(80)]
        public string Categoria { get; set; } = string.Empty; // categoria (ex.: "Servico de Manutencao")

        [Required, StringLength(250)]
        public string Descricao { get; set; } = string.Empty; // descrição curta do lançamento

        public decimal Valor { get; set; } // valor SEMPRE positivo; o Tipo diz se entrou ou saiu

        [StringLength(120)]
        public string? PagadorRecebedor { get; set; } // quem pagou (entrada) ou foi pago (saída)

        public PaymentMethod FormaPagamento { get; set; } // Pix, dinheiro, cartão...

        [StringLength(120)]
        public string? ServicoPeca { get; set; } // serviço ou peça relacionada (opcional)

        [StringLength(500)]
        public string? Observacoes { get; set; } // observações livres

        public EntryOrigin Origem { get; set; } = EntryOrigin.Manual; // de onde veio o lançamento

        [Required, StringLength(60)]
        public string Socio { get; set; } = "Empresa"; // sócio relacionado (ou "Empresa")

        [StringLength(20)]
        public string? NumeroOs { get; set; } // código da OS (ex.: OS-00012) só para consulta rápida

        public int? ServiceOrderId { get; set; } // OS ligada a este lançamento (opcional)
        public ServiceOrder? ServiceOrder { get; set; } // a OS em si

        public int? StockItemId { get; set; } // peça de estoque ligada (opcional)
        public StockItem? StockItem { get; set; } // a peça em si

        public DateTime CreatedAt { get; set; } // quando o registro foi criado no sistema
    }
}
