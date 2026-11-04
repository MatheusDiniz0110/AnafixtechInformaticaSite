// ---------- Importações: classes que este controller utiliza ----------
using AnafixtechInformaticaSite.Data;                  // AppDbContext (banco de dados)
using AnafixtechInformaticaSite.Models;                // ServiceRequest, Notification...
using AnafixtechInformaticaSite.Models.ViewModels;     // formulário público de solicitação
using AnafixtechInformaticaSite.Services;              // Clock (hora de Brasília)
using Microsoft.AspNetCore.Authorization;              // [AllowAnonymous]
using Microsoft.AspNetCore.Mvc;                        // Controller, IActionResult...
using Microsoft.EntityFrameworkCore;                   // FirstOrDefaultAsync

namespace AnafixtechInformaticaSite.Controllers
{
    // ============================================================================================
    // ServiceRequestController — somente o FORMULÁRIO PÚBLICO de solicitação de serviço.
    //
    // A parte administrativa (lista, detalhes, edição, exclusão e conversão em OS) fica na
    // dashboard, na aba "Solicitações" do DashboardController: assim existe um só lugar para
    // a equipe acompanhar os pedidos, sem duplicar telas nem dados.
    // ============================================================================================
    [Authorize(Policy = "Admin")]   // por padrão, tudo aqui exige login de administrador
    [AutoValidateAntiforgeryToken]  // todo POST precisa do token anti-falsificação (o formulário envia)
    public class ServiceRequestController : Controller
    {
        // Acesso ao banco, recebido por injeção de dependência.
        private readonly AppDbContext _context;

        public ServiceRequestController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================================================
        // FORMULÁRIO PÚBLICO
        // =========================================================================================

        // Abre o formulário.  →  GET /ServiceRequest/Create  (ou /ServiceRequest/Create?serviceId=3)
        [AllowAnonymous] // sobrescreve o [Authorize] da classe: visitante não logado pode entrar
        [HttpGet]
        public async Task<IActionResult> Create(int? serviceId)
        {
            var vm = new ServiceRequestFormViewModel();

            // Se veio da página de serviços, já deixa o serviço escolhido.
            if (serviceId.HasValue) vm.ServiceId = serviceId;

            // Se a pessoa está logada, aproveita nome e e-mail da conta.
            await PreencherDadosDoUsuarioLogadoAsync(vm);
            await CarregarServicosAsync(vm);

            return View(vm);
        }

        // Recebe o formulário público.  →  POST /ServiceRequest/Create
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Create(ServiceRequestFormViewModel vm)
        {
            // O consentimento é obrigatório: sem ele não podemos usar os dados para responder.
            if (!vm.AcceptPrivacy)
                ModelState.AddModelError(nameof(vm.AcceptPrivacy), "É preciso autorizar o contato para enviar a solicitação.");

            // Confere se o serviço escolhido ainda existe no catálogo.
            Service? servico = null;
            if (vm.ServiceId.HasValue)
            {
                servico = await _context.Services.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == vm.ServiceId.Value);

                if (servico == null)
                    ModelState.AddModelError(nameof(vm.ServiceId), "O serviço escolhido não existe mais.");
            }

            // Se algum campo estiver inválido, volta ao formulário mostrando as mensagens.
            if (!ModelState.IsValid)
            {
                await CarregarServicosAsync(vm);
                return View(vm);
            }

            // Monta a solicitação com os dados digitados.
            var solicitacao = new ServiceRequest
            {
                // Dados de contato
                ContactName = vm.ContactName.Trim(),
                ContactEmail = vm.ContactEmail.Trim().ToLowerInvariant(),
                ContactPhone = vm.ContactPhone.Trim(),
                ContactAddress = string.IsNullOrWhiteSpace(vm.ContactAddress) ? null : vm.ContactAddress.Trim(),
                PickupRequested = vm.PickupRequested,

                // Equipamento e problema
                Device = vm.Device.Trim(),
                Description = vm.Description.Trim(),
                ServiceId = servico?.Id,

                // Valor de referência do catálogo (o técnico pode ajustar depois).
                Price = servico?.Price,

                // Controle
                Status = RequestStatus.Pending,   // toda solicitação nasce pendente
                RequestDate = Clock.Now,          // hora de Brasília
                UserId = ObterUsuarioLogadoId()   // null quando o visitante não está logado
            };

            _context.ServiceRequests.Add(solicitacao);
            await _context.SaveChangesAsync(); // grava para a solicitação ganhar um Id/protocolo

            // Avisa os administradores no sino da dashboard.
            _context.Notifications.Add(new Notification
            {
                Type = NotificationType.OsNew,
                Title = "Nova solicitação de serviço",
                Message = $"{solicitacao.Protocolo}: {solicitacao.ContactName} — {solicitacao.Device}",
                RefId = solicitacao.Id,
                RefType = "request",
                IsRead = false,
                CreatedAt = Clock.Now
            });
            await _context.SaveChangesAsync();

            // Leva os dados para a tela de confirmação (aparecem uma única vez).
            TempData["Protocolo"] = solicitacao.Protocolo;
            TempData["NomeSolicitante"] = solicitacao.ContactName;

            return RedirectToAction(nameof(Sucesso));
        }

        // Confirmação exibida ao cliente depois do envio.  →  GET /ServiceRequest/Sucesso
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Sucesso()
        {
            // Se alguém abrir a página direto (sem ter enviado), volta ao formulário.
            if (TempData["Protocolo"] is not string protocolo)
                return RedirectToAction(nameof(Create));

            ViewBag.Protocolo = protocolo;                              // ex.: SOL-00012
            ViewBag.NomeSolicitante = TempData["NomeSolicitante"] as string;

            return View();
        }

        // =========================================================================================
        // MÉTODOS AUXILIARES (privados)
        // =========================================================================================

        // Preenche a lista de serviços do <select>, em ordem alfabética.
        private async Task CarregarServicosAsync(ServiceRequestFormViewModel vm)
        {
            vm.Servicos = await _context.Services.AsNoTracking()
                .OrderBy(s => s.Title)
                .ToListAsync();
        }

        // Se o visitante está logado, aproveita nome e e-mail da conta no formulário.
        private async Task PreencherDadosDoUsuarioLogadoAsync(ServiceRequestFormViewModel vm)
        {
            var id = ObterUsuarioLogadoId();
            if (id == null) return;

            var usuario = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id.Value);

            if (usuario == null) return;

            vm.ContactName = usuario.Name;
            vm.ContactEmail = usuario.Email;
        }

        // Lê o Id do usuário logado. O token JWT do site guarda o Id na claim "id".
        private int? ObterUsuarioLogadoId()
        {
            var texto = User.FindFirst("id")?.Value;
            return int.TryParse(texto, out var id) ? id : null;
        }
    }
}
