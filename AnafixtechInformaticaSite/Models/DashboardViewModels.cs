using System.ComponentModel.DataAnnotations;                 // atributos de validação ([Required], [Display]...)
using Microsoft.AspNetCore.Mvc.ModelBinding;                 // [BindNever]: impede o formulário de preencher a propriedade
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;      // [ValidateNever]: não valida a propriedade

// View models são classes "sob medida" para cada tela: levam do controller para a view
// só os dados que a tela precisa (ou trazem da tela os campos do formulário).
namespace AnafixtechInformaticaSite.Models.ViewModels
{
    // =====================================================================
    // VISÃO GERAL
    // =====================================================================
    public class DashboardIndexViewModel
    {
        public Dictionary<OrderStatus, int> PedidosPorStatus { get; set; } = new(); // quantidade de OS em cada status
        public int PedidosNovos { get; set; }        // OS com status "Novo"
        public int PedidosEmAndamento { get; set; }  // OS aguardando ou em andamento
        public int EntregasNoMes { get; set; }       // OS entregues no mês atual
        public decimal EntradasMes { get; set; }     // total que entrou no mês
        public decimal SaidasMes { get; set; }       // total que saiu no mês
        public decimal SaldoMes => EntradasMes - SaidasMes; // saldo do mês (calculado)
        public decimal AReceber { get; set; }        // valor ainda não recebido das OS abertas
        public int TicketsAbertos { get; set; }      // tickets pendentes ou em atendimento
        public List<ServiceOrder> UltimosPedidos { get; set; } = new();       // 6 OS mais recentes
        public List<Agendamento> ProximosAgendamentos { get; set; } = new();  // próximos compromissos
        public List<StockItem> EstoqueBaixo { get; set; } = new();            // peças no mínimo ou abaixo
        public List<SupportTicket> TicketsEmAberto { get; set; } = new();     // tickets ainda abertos
        public List<Notification> NotificacoesRecentes { get; set; } = new(); // últimas notificações
    }

    // =====================================================================
    // PEDIDOS (OS)
    // =====================================================================
    public class PedidosViewModel
    {
        public List<ServiceOrder> Pedidos { get; set; } = new();                  // OS que aparecem na tabela
        public OrderStatus? Status { get; set; }                                  // filtro de status (nulo = todos)
        public string? Busca { get; set; }                                        // texto digitado na busca
        public Dictionary<OrderStatus, int> ContagemPorStatus { get; set; } = new(); // números nos botões de filtro
    }

    // Formulário de criação manual de OS pelo painel.
    public class PedidoFormViewModel
    {
        [Display(Name = "Nome do cliente")]
        [Required(ErrorMessage = "Informe o nome do cliente."), StringLength(120)]
        public string ClientName { get; set; } = string.Empty;

        [Display(Name = "E-mail")]
        [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), StringLength(160)]
        public string ClientEmail { get; set; } = string.Empty;

        [Display(Name = "Telefone / WhatsApp")]
        [Required(ErrorMessage = "Informe o telefone."), StringLength(30)]
        public string ClientPhone { get; set; } = string.Empty;

        [Display(Name = "Endereço")]
        [StringLength(250)]
        public string? ClientAddress { get; set; } // opcional

        [Display(Name = "Buscar o equipamento")]
        public bool PickupRequested { get; set; } // marcado = vamos buscar

        [Display(Name = "Equipamento")]
        [Required(ErrorMessage = "Informe o equipamento."), StringLength(120)]
        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "Problema relatado")]
        [Required(ErrorMessage = "Descreva o problema."), StringLength(2000)]
        public string ProblemDescription { get; set; } = string.Empty;

        [Display(Name = "Urgência")]
        public UrgencyLevel Urgency { get; set; } = UrgencyLevel.Normal;

        [Display(Name = "Serviço do catálogo")]
        public int? ServiceId { get; set; } // opcional

        [Display(Name = "Preço estimado")]
        public string? EstimatedPrice { get; set; } // texto digitado; convertido com MoneyParser

        // Lista usada só para montar o <select> da tela; nunca vem do formulário.
        [BindNever, ValidateNever]
        public List<Service> Servicos { get; set; } = new();
    }

    // Tudo que a tela de detalhes de uma OS precisa.
    public class PedidoDetalhesViewModel
    {
        public ServiceOrder Pedido { get; set; } = new();                // a OS (com histórico e serviço carregados)
        public List<FinancialEntry> Lancamentos { get; set; } = new();   // pagamentos ligados à OS
        public List<StockItem> Pecas { get; set; } = new();              // peças para o campo "peça usada"
    }

    // =====================================================================
    // FINANCEIRO
    // =====================================================================
    public class CategoriaTotal
    {
        public EntryType Tipo { get; set; }                      // entrada ou saída
        public string Categoria { get; set; } = string.Empty;    // nome da categoria
        public decimal Total { get; set; }                       // soma dos valores
    }

    public class SocioTotal
    {
        public string Socio { get; set; } = string.Empty; // nome do sócio
        public decimal Entradas { get; set; }             // quanto entrou para ele
        public decimal Saidas { get; set; }               // quanto saiu por ele
    }

    public class FinanceiroViewModel
    {
        public DateOnly De { get; set; }            // início do período
        public DateOnly Ate { get; set; }           // fim do período
        public EntryType? Tipo { get; set; }        // filtro de tipo (nulo = ambos)
        public string? Socio { get; set; }          // filtro de sócio (nulo = todos)
        public decimal Entradas { get; set; }       // total de entradas do período
        public decimal Saidas { get; set; }         // total de saídas do período
        public decimal Saldo => Entradas - Saidas;  // saldo (calculado)
        public List<CategoriaTotal> PorCategoria { get; set; } = new(); // resumo por categoria
        public List<SocioTotal> PorSocio { get; set; } = new();         // resumo por sócio
        public List<FinancialEntry> Lancamentos { get; set; } = new();  // linhas da tabela
        public bool Cortado { get; set; }           // true = havia mais linhas do que o limite exibido
    }

    // Formulário de lançamento financeiro (novo ou edição).
    public class LancamentoFormViewModel
    {
        // 0 = novo lançamento; maior que zero = lançamento sendo editado.
        public int Id { get; set; }

        // Origem gravada no banco. Só serve para a tela avisar quando o lançamento
        // foi gerado por uma OS ou por uma compra de estoque (não vem do formulário).
        [BindNever, ValidateNever]
        public EntryOrigin? Origem { get; set; }

        [Display(Name = "Data")]
        [Required(ErrorMessage = "Informe a data.")]
        public DateOnly? Data { get; set; }

        [Display(Name = "Tipo")]
        public EntryType Tipo { get; set; } = EntryType.Entrada;

        [Display(Name = "Categoria")]
        [Required(ErrorMessage = "Informe a categoria."), StringLength(80)]
        public string Categoria { get; set; } = string.Empty;

        [Display(Name = "Descrição")]
        [Required(ErrorMessage = "Informe a descrição."), StringLength(250)]
        public string Descricao { get; set; } = string.Empty;

        [Display(Name = "Valor")]
        [Required(ErrorMessage = "Informe o valor (ex.: 120,50).")]
        public string Valor { get; set; } = string.Empty; // texto; convertido com MoneyParser

        [Display(Name = "Quem pagou / foi pago")]
        [StringLength(120)]
        public string? PagadorRecebedor { get; set; }

        [Display(Name = "Forma de pagamento")]
        public PaymentMethod FormaPagamento { get; set; } = PaymentMethod.Pix;

        [Display(Name = "Serviço ou peça")]
        [StringLength(120)]
        public string? ServicoPeca { get; set; }

        [Display(Name = "Observações")]
        [StringLength(500)]
        public string? Observacoes { get; set; }

        [Display(Name = "Sócio")]
        [Required, StringLength(60)]
        public string Socio { get; set; } = "Empresa";
    }

    // =====================================================================
    // ESTOQUE
    // =====================================================================
    public class EstoqueViewModel
    {
        public List<StockItem> Itens { get; set; } = new(); // peças da tabela
        public bool SomenteBaixo { get; set; }              // filtro "só estoque baixo"
        public string? Busca { get; set; }                  // texto da busca
    }

    // Formulário de nova peça.
    public class EstoqueFormViewModel
    {
        [Required(ErrorMessage = "Informe o nome da peça."), StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(40)]
        public string Category { get; set; } = "outro";

        [Range(0, 100000, ErrorMessage = "Quantidade inválida.")]
        public int Quantity { get; set; }

        [Range(0, 100000, ErrorMessage = "Mínimo inválido.")]
        public int MinQuantity { get; set; }

        public string? PurchasePrice { get; set; } // texto; convertido com MoneyParser

        [StringLength(120)] public string? Supplier { get; set; }
        [StringLength(120)] public string? Location { get; set; }
        [StringLength(500)] public string? Description { get; set; }
    }

    // =====================================================================
    // SERVIÇOS (catálogo exibido no site)
    // =====================================================================
    // Uma linha da lista: o serviço + se ele está ativo (a situação é gravada como texto).
    public class ServicoResumo
    {
        public Service Servico { get; set; } = new();
        public bool Ativo { get; set; }
    }

    public class ServicosViewModel
    {
        public List<ServicoResumo> Servicos { get; set; } = new(); // serviços da tabela
        public string? Busca { get; set; }                         // texto da busca
        public int Ativos => Servicos.Count(s => s.Ativo);         // contador exibido no topo
        public int Inativos => Servicos.Count(s => !s.Ativo);
    }

    // Formulário de serviço (novo ou edição).
    public class ServicoFormViewModel
    {
        // 0 = novo serviço; maior que zero = serviço sendo editado.
        public int Id { get; set; }

        [Display(Name = "Categoria")]
        [Required(ErrorMessage = "Informe a categoria."), StringLength(40)]
        public string Class { get; set; } = "Software";

        [Display(Name = "Nome do serviço")]
        [Required(ErrorMessage = "Informe o nome do serviço."), StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Descrição")]
        [Required(ErrorMessage = "Informe a descrição."), StringLength(600)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Preço")]
        [Required(ErrorMessage = "Informe o preço (ex.: 120,50).")]
        public string Price { get; set; } = string.Empty; // texto; convertido com MoneyParser

        [Display(Name = "Serviço ativo no site")]
        public bool Ativo { get; set; } = true;
    }

    // =====================================================================
    // AGENDA
    // =====================================================================
    public class AgendaViewModel
    {
        public List<Agendamento> Agendamentos { get; set; } = new();     // compromissos
        public List<HorarioDisponivel> Horarios { get; set; } = new();   // horários disponíveis
        public List<ServiceOrder> PedidosAbertos { get; set; } = new();  // OS para vincular ao agendamento
    }

    // Formulário de novo agendamento.
    public class AgendamentoFormViewModel
    {
        public AgendamentoTipo Tipo { get; set; } = AgendamentoTipo.Visita;

        public int? ServiceOrderId { get; set; } // OS ligada (opcional)

        [Required(ErrorMessage = "Informe o técnico."), StringLength(80)]
        public string Tecnico { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a data e a hora.")]
        public DateTime? DataHora { get; set; }

        [Required(ErrorMessage = "Informe a descrição."), StringLength(300)]
        public string Descricao { get; set; } = string.Empty;

        [StringLength(250)] public string? Endereco { get; set; }
        [StringLength(120)] public string? ClientName { get; set; }
        [StringLength(30)] public string? ClientPhone { get; set; }
    }

    // Formulário de novo horário disponível.
    public class HorarioFormViewModel
    {
        [Required(ErrorMessage = "Informe a data e a hora.")]
        public DateTime? DataHora { get; set; }

        [Required(ErrorMessage = "Informe o técnico."), StringLength(80)]
        public string Tecnico { get; set; } = string.Empty;

        [Range(15, 480, ErrorMessage = "A duração deve ficar entre 15 e 480 minutos.")]
        public int DuracaoMinutos { get; set; } = 60;

        [StringLength(300)] public string? Observacao { get; set; }
    }

    // =====================================================================
    // TICKETS
    // =====================================================================
    public class TicketsViewModel
    {
        public List<SupportTicket> Tickets { get; set; } = new(); // tickets da tabela
        public TicketStatus? Status { get; set; }                 // filtro de status (nulo = todos)
    }

    // Formulário de novo ticket.
    public class TicketFormViewModel
    {
        [Required(ErrorMessage = "Informe o nome do cliente."), StringLength(120)]
        public string ClientName { get; set; } = string.Empty;

        [StringLength(30)] public string? ClientPhone { get; set; }

        [Required(ErrorMessage = "Informe o assunto."), StringLength(150)]
        public string Subject { get; set; } = string.Empty;

        [StringLength(2000)] public string? Description { get; set; }

        public TicketPriority Priority { get; set; } = TicketPriority.Medium;

        [StringLength(80)] public string? AssignedTo { get; set; }
    }

    // =====================================================================
    // NOTIFICAÇÕES
    // =====================================================================
    public class NotificacoesViewModel
    {
        public List<Notification> Notificacoes { get; set; } = new(); // lista exibida
    }
}
