using System.ComponentModel.DataAnnotations;                 // atributos de validação ([Required], [EmailAddress]...)
using Microsoft.AspNetCore.Mvc.ModelBinding;                 // [BindNever]
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;      // [ValidateNever]

// View models da solicitação de serviço: classes "sob medida" para cada tela.
namespace AnafixtechInformaticaSite.Models.ViewModels
{
    // Formulário PÚBLICO de solicitação (qualquer visitante pode preencher).
    public class ServiceRequestFormViewModel
    {
        [Display(Name = "Seu nome")]
        [Required(ErrorMessage = "Informe seu nome."), StringLength(120)]
        public string ContactName { get; set; } = string.Empty;

        [Display(Name = "E-mail")]
        [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), StringLength(160)]
        public string ContactEmail { get; set; } = string.Empty;

        [Display(Name = "Telefone / WhatsApp")]
        [Required(ErrorMessage = "Informe um telefone ou WhatsApp."), StringLength(30)]
        public string ContactPhone { get; set; } = string.Empty;

        [Display(Name = "Endereço")]
        [StringLength(250)]
        public string? ContactAddress { get; set; } // só é útil quando pedimos para buscar o equipamento

        [Display(Name = "Buscar o equipamento no meu endereço")]
        public bool PickupRequested { get; set; }

        [Display(Name = "Equipamento")]
        [Required(ErrorMessage = "Informe o equipamento."), StringLength(120)]
        public string Device { get; set; } = string.Empty;

        [Display(Name = "Serviço desejado")]
        public int? ServiceId { get; set; } // opcional: nem todo mundo sabe qual serviço precisa

        [Display(Name = "Descreva o problema")]
        [Required(ErrorMessage = "Descreva o problema."), StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        // Consentimento para usar os dados no atendimento (boa prática de privacidade/LGPD).
        [Display(Name = "Autorizo o contato e o uso destes dados para atendimento.")]
        public bool AcceptPrivacy { get; set; }

        // Lista usada apenas para montar o <select> da tela; nunca vem do formulário.
        [BindNever, ValidateNever]
        public List<Service> Servicos { get; set; } = new();
    }

    // Formulário do ADMINISTRADOR para corrigir/atualizar uma solicitação.
    public class ServiceRequestEditViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Nome do cliente")]
        [Required(ErrorMessage = "Informe o nome do cliente."), StringLength(120)]
        public string ContactName { get; set; } = string.Empty;

        [Display(Name = "E-mail")]
        [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), StringLength(160)]
        public string ContactEmail { get; set; } = string.Empty;

        [Display(Name = "Telefone / WhatsApp")]
        [Required(ErrorMessage = "Informe o telefone."), StringLength(30)]
        public string ContactPhone { get; set; } = string.Empty;

        [Display(Name = "Endereço")]
        [StringLength(250)]
        public string? ContactAddress { get; set; }

        [Display(Name = "Buscar o equipamento")]
        public bool PickupRequested { get; set; }

        [Display(Name = "Equipamento")]
        [Required(ErrorMessage = "Informe o equipamento."), StringLength(120)]
        public string Device { get; set; } = string.Empty;

        [Display(Name = "Serviço do catálogo")]
        public int? ServiceId { get; set; }

        [Display(Name = "Problema relatado")]
        [Required(ErrorMessage = "Descreva o problema."), StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Situação")]
        public RequestStatus Status { get; set; } = RequestStatus.Pending;

        [Display(Name = "Valor orçado")]
        public string? Price { get; set; } // texto digitado; convertido com MoneyParser (vazio = a combinar)

        [Display(Name = "Concluída em")]
        public DateOnly? CompletionDate { get; set; }

        [Display(Name = "Anotações internas (não aparecem para o cliente)")]
        [StringLength(2000)]
        public string? InternalNotes { get; set; }

        // Lista do <select> de serviços.
        [BindNever, ValidateNever]
        public List<Service> Servicos { get; set; } = new();
    }

    // Tudo que a lista administrativa precisa: linhas, filtro, busca e contadores.
    public class ServiceRequestsViewModel
    {
        public List<ServiceRequest> Solicitacoes { get; set; } = new();
        public RequestStatus? Status { get; set; }                                  // filtro atual (nulo = todas)
        public string? Busca { get; set; }                                          // texto digitado
        public Dictionary<RequestStatus, int> ContagemPorStatus { get; set; } = new(); // números dos botões
    }
}
