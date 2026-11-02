using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnafixtechInformaticaSite.Models
{
    // Ticket de suporte (entidade "SupportTicket" do Base44): dúvidas e problemas dos clientes.
    [Table("SupportTickets")]
    public class SupportTicket
    {
        [Key]
        public int Id { get; set; } // chave primária

        [Required, StringLength(120)]
        public string ClientName { get; set; } = string.Empty; // nome do cliente

        [StringLength(30)]
        public string? ClientPhone { get; set; } // telefone do cliente

        [Required, StringLength(150)]
        public string Subject { get; set; } = string.Empty; // assunto

        [StringLength(2000)]
        public string? Description { get; set; } // descrição detalhada

        public TicketPriority Priority { get; set; } = TicketPriority.Medium; // prioridade

        public TicketStatus Status { get; set; } = TicketStatus.Pending; // situação

        [StringLength(4000)]
        public string? CommunicationLog { get; set; } // histórico de contatos (texto acumulado)

        [StringLength(80)]
        public string? AssignedTo { get; set; } // técnico responsável

        public DateTime CreatedAt { get; set; } // abertura
        public DateTime? ResolvedAt { get; set; } // quando foi resolvido (nulo enquanto aberto)
    }
}
