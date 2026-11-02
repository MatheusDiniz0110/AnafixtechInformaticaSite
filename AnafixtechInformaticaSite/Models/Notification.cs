using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnafixtechInformaticaSite.Models
{
    // Notificação do painel (sino): avisa de nova OS, pagamento, estoque baixo, ticket...
    // Entidade "Notification" do Base44.
    [Table("Notifications")]
    public class Notification
    {
        [Key]
        public int Id { get; set; } // chave primária

        [Required, StringLength(120)]
        public string Title { get; set; } = string.Empty; // título curto

        [Required, StringLength(500)]
        public string Message { get; set; } = string.Empty; // mensagem

        public NotificationType Type { get; set; } = NotificationType.Info; // categoria

        public int? RefId { get; set; } // Id do item relacionado (OS, ticket, peça...)

        [StringLength(20)]
        public string? RefType { get; set; } // tipo do item: "order", "ticket" ou "stock"

        public bool IsRead { get; set; } // true = já foi lida

        public DateTime CreatedAt { get; set; } // quando foi criada
    }
}
