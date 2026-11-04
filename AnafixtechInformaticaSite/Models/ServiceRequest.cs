using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnafixtechInformaticaSite.Models
{
    // Solicitação de serviço: o pedido que qualquer visitante pode enviar pelo site,
    // mesmo sem ter conta. É a "porta de entrada" do cliente; depois o técnico analisa
    // e pode transformar a solicitação em uma Ordem de Serviço (ServiceOrder).
    //
    // Diferente da OS, aqui os dados de contato ficam na própria solicitação:
    // assim o lead vindo do WhatsApp ou de um visitante anônimo não se perde.
    [Table("ServiceRequests")] // nome da tabela no SQL Server
    public class ServiceRequest
    {
        [Key] // chave primária; o banco gera o número sozinho (1, 2, 3...)
        public int Id { get; set; }

        // Código amigável mostrado ao cliente (ex.: SOL-00012). Não vira coluna no banco.
        [NotMapped]
        public string Protocolo => $"SOL-{Id:D5}";

        // ---------- Vínculo com a conta do site (opcional) ----------
        // Preenchido apenas quando a pessoa está logada ao enviar o formulário.
        // Visitantes anônimos deixam este campo nulo e são identificados pelos dados de contato.
        public int? UserId { get; set; }
        public User? User { get; set; } // a conta em si (o EF preenche com Include)

        // ---------- Dados de contato (sempre preenchidos no formulário público) ----------
        [Required(ErrorMessage = "Informe seu nome."), StringLength(120)]
        public string ContactName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), StringLength(160)]
        public string ContactEmail { get; set; } = string.Empty; // necessário para a OS e para enviar o orçamento

        [Required(ErrorMessage = "Informe um telefone ou WhatsApp."), StringLength(30)]
        public string ContactPhone { get; set; } = string.Empty;

        [StringLength(250)]
        public string? ContactAddress { get; set; } // usado quando o cliente pede para buscar o equipamento

        public bool PickupRequested { get; set; } // true = quer que a AnaFixTech busque o equipamento

        // ---------- Equipamento e problema ----------
        [Required(ErrorMessage = "Informe o equipamento."), StringLength(120)]
        public string Device { get; set; } = string.Empty; // ex.: Notebook Dell Inspiron

        [Required(ErrorMessage = "Descreva o problema."), StringLength(2000)]
        public string Description { get; set; } = string.Empty; // o que está acontecendo

        // Serviço do catálogo escolhido (opcional). Quando existe, já sabemos qual é o assunto.
        public int? ServiceId { get; set; }
        public Service? Service { get; set; }

        // ---------- Andamento ----------
        public RequestStatus Status { get; set; } = RequestStatus.Pending; // toda solicitação nasce "Pendente"

        // Valor estimado/orçado. Nulo = "a combinar". É preenchido pelo técnico, nunca pelo visitante.
        public decimal? Price { get; set; }

        // Anotações internas: só aparecem na dashboard, nunca para o cliente.
        [StringLength(2000)]
        public string? InternalNotes { get; set; }

        public DateTime RequestDate { get; set; } = DateTime.Now; // quando a solicitação chegou
        public DateOnly? CompletionDate { get; set; }             // quando foi concluída/cancelada

        // ---------- Conversão em OS ----------
        // Depois que a solicitação vira uma Ordem de Serviço, guardamos o vínculo para
        // não converter a mesma solicitação duas vezes.
        public int? ServiceOrderId { get; set; }
        public ServiceOrder? ServiceOrder { get; set; }
    }
}
