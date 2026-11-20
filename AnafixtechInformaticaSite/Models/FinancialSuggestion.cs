using System.ComponentModel.DataAnnotations;        // atributos de validação: [Key], [Required], [StringLength]
using System.ComponentModel.DataAnnotations.Schema; // atributos de banco: [Table]

namespace AnafixtechInformaticaSite.Models
{
    // Sugestão de recebimento gerada a partir de uma OS.
    //
    // REGRA DE OURO: esta tabela NÃO é o financeiro. Ela só guarda o que o sistema
    // acha que deveria entrar. Nenhum valor aparece no financeiro enquanto o
    // administrador não clicar em OK (aí sim criamos um FinancialEntry).
    //
    // Fica numa tabela separada para podermos ter no máximo UMA sugestão pendente
    // por OS e para guardar o histórico do que foi aprovado, ignorado ou cancelado.
    [Table("FinancialSuggestions")]
    public class FinancialSuggestion
    {
        [Key]
        public int Id { get; set; } // chave primária

        // A OS é obrigatória: toda sugestão nasce de uma OS e para sempre aponta para ela.
        public int ServiceOrderId { get; set; }
        public ServiceOrder? ServiceOrder { get; set; } // a OS (preenchida com Include)

        public decimal Valor { get; set; } // valor sugerido, sempre positivo e com 2 casas

        public DateOnly DataSugerida { get; set; } // dia sugerido para o recebimento (hora de Brasília)

        public PaymentMethod FormaPagamentoSugerida { get; set; } = PaymentMethod.Pix; // forma sugerida

        [Required, StringLength(60)]
        public string SocioSugerido { get; set; } = "Empresa"; // sócio sugerido (padrão da empresa)

        [Required, StringLength(80)]
        public string Categoria { get; set; } = string.Empty; // categoria do futuro lançamento

        [Required, StringLength(250)]
        public string Descricao { get; set; } = string.Empty; // texto do futuro lançamento

        [Required, StringLength(120)]
        public string MotivoCriacao { get; set; } = string.Empty; // ex.: "OS concluída", "Preço alterado"

        public SuggestionStatus Status { get; set; } = SuggestionStatus.Pendente;

        public DateTime CriadaEm { get; set; } // quando a sugestão foi criada

        public DateTime? DecididaEm { get; set; } // quando houve OK / ignore / cancelamento

        [StringLength(120)]
        public string? DecididoPor { get; set; } // quem tomou a decisão (nome do painel)

        [StringLength(500)]
        public string? MotivoRejeicao { get; set; } // motivo curto quando o usuário ignora

        // Vínculo com o lançamento criado no ato da aprovação. Nulo enquanto não há OK.
        public int? FinancialEntryId { get; set; }
        public FinancialEntry? FinancialEntry { get; set; }

        // Token de concorrência: vai no formulário escondido e impede que duas abas
        // (ou um duplo clique) aprovem a mesma sugestão. Troca de valor a cada decisão.
        public Guid Token { get; set; } = Guid.NewGuid();

        // "Assinatura" da sugestão. Serve para a regra R6: uma sugestão ignorada
        // só volta a aparecer se o valor combinado ou o já recebido da OS mudar.
        // São cópias do momento da criação, nunca recalculadas depois.
        public decimal ValorCombinadoNaCriacao { get; set; }
        public decimal ValorRecebidoNaCriacao { get; set; }
    }
}
