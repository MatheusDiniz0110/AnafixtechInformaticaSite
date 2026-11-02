using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnafixtechInformaticaSite.Models
{
    // Uma linha do histórico de status de uma OS (equivale ao "status_history" do Base44).
    // Cada mudança de status gera um registro aqui: assim nunca se perde quem mudou, quando e por quê.
    [Table("StatusHistory")]
    public class StatusHistoryEntry
    {
        [Key]
        public int Id { get; set; } // chave primária

        public int ServiceOrderId { get; set; } // a qual OS este registro pertence (chave estrangeira)
        public ServiceOrder? ServiceOrder { get; set; } // a OS em si

        public OrderStatus? FromStatus { get; set; } // status anterior (nulo na criação da OS)
        public OrderStatus ToStatus { get; set; } // novo status

        public DateTime ChangedAt { get; set; } // quando mudou

        [StringLength(120)]
        public string? ChangedBy { get; set; } // quem mudou (nome do usuário logado)

        [StringLength(500)]
        public string? Note { get; set; } // observação/motivo da mudança
    }
}
