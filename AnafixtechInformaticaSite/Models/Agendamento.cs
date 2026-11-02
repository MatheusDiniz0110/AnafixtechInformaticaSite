using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnafixtechInformaticaSite.Models
{
    // Compromisso da agenda (entidade "Agendamento" do Base44).
    [Table("Agendamentos")]
    public class Agendamento
    {
        [Key]
        public int Id { get; set; } // chave primária

        public AgendamentoTipo Tipo { get; set; } = AgendamentoTipo.OS; // OS, Visita ou Outro

        public int? ServiceOrderId { get; set; } // OS relacionada (opcional)
        public ServiceOrder? ServiceOrder { get; set; } // a OS em si

        [Required, StringLength(80)]
        public string Tecnico { get; set; } = string.Empty; // técnico responsável

        public DateTime DataHora { get; set; } // quando acontece

        [Required, StringLength(300)]
        public string Descricao { get; set; } = string.Empty; // o que será feito

        [StringLength(250)]
        public string? Endereco { get; set; } // endereço (para visitas)

        [StringLength(120)]
        public string? ClientName { get; set; } // nome do cliente

        [StringLength(30)]
        public string? ClientPhone { get; set; } // telefone do cliente

        public AgendamentoStatus Status { get; set; } = AgendamentoStatus.Agendado; // situação

        public DateTime CreatedAt { get; set; } // quando foi cadastrado
    }
}
