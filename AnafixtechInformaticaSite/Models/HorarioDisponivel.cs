using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnafixtechInformaticaSite.Models
{
    // Horário que o cliente poderá escolher ao agendar (entidade "HorarioDisponivel" do Base44).
    [Table("HorariosDisponiveis")]
    public class HorarioDisponivel
    {
        [Key]
        public int Id { get; set; } // chave primária

        public DateTime DataHora { get; set; } // início do horário

        [Required, StringLength(80)]
        public string Tecnico { get; set; } = string.Empty; // técnico responsável pelo horário

        public int DuracaoMinutos { get; set; } = 60; // duração estimada

        public SlotStatus Status { get; set; } = SlotStatus.Disponivel; // livre, reservado ou bloqueado

        public int? AgendamentoId { get; set; } // agendamento que reservou este horário (opcional)
        public Agendamento? Agendamento { get; set; } // o agendamento em si

        [StringLength(120)]
        public string? ClientName { get; set; } // cliente que reservou

        [StringLength(30)]
        public string? ClientPhone { get; set; } // telefone de quem reservou

        [StringLength(300)]
        public string? Observacao { get; set; } // observação do técnico
    }
}
