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

        // Acesso às pastas do site (wwwroot) para salvar a foto do equipamento.
        private readonly IWebHostEnvironment _ambiente;

        public ServiceRequestController(AppDbContext context, IWebHostEnvironment ambiente)
        {
            _context = context;
            _ambiente = ambiente;
        }

        // Limite do arquivo de foto aceito no formulário público (5 MB).
        private const long TamanhoMaximoFoto = 5 * 1024 * 1024;

        // Extensões aceitas. A checagem real também olha os bytes iniciais do arquivo,
        // porque só o nome/extensão pode ser falsificado.
        private static readonly string[] ExtensoesFotoPermitidas = { ".jpg", ".jpeg", ".png", ".webp" };

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
            // Armadilha anti-spam: o campo "Website" fica escondido por CSS. Se veio preenchido,
            // quem enviou foi um robô. Respondemos como se tivesse dado certo, sem gravar nada,
            // para não ensinar ao robô que a armadilha funcionou.
            if (!string.IsNullOrWhiteSpace(vm.Website))
            {
                TempData["Protocolo"] = "";
                TempData["NomeSolicitante"] = "";
                return RedirectToAction(nameof(Sucesso));
            }

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

            // ---------- Foto do equipamento (opcional) ----------
            // Só chega aqui se o restante do formulário estiver válido, para não gravar
            // arquivo de um envio que será recusado.
            string? caminhoFoto = null;
            if (vm.Photo is not null && vm.Photo.Length > 0)
            {
                caminhoFoto = await SalvarFotoAsync(vm.Photo);

                // O método devolve null quando o arquivo é inválido e já registrou o erro.
                if (caminhoFoto is null)
                {
                    await CarregarServicosAsync(vm);
                    return View(vm);
                }
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
                UserId = ObterUsuarioLogadoId(),  // null quando o visitante não está logado
                PhotoPath = caminhoFoto           // null quando não anexou foto
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
        // ACOMPANHAMENTO PÚBLICO POR PROTOCOLO
        // =========================================================================================

        // Abre a página de consulta.  →  GET /ServiceRequest/Acompanhar
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Acompanhar()
        {
            return View(new ServiceRequestTrackingViewModel());
        }

        // Recebe protocolo + e-mail e mostra a situação atual.  →  POST /ServiceRequest/Acompanhar
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Acompanhar(ServiceRequestTrackingViewModel vm)
        {
            vm.Buscou = true;

            if (!ModelState.IsValid) return View(vm);

            // O protocolo é "SOL-00012". Extraímos apenas os números para chegar ao Id.
            var digitos = new string(vm.Protocolo.Where(char.IsDigit).ToArray());
            if (!int.TryParse(digitos, out var id))
            {
                ModelState.AddModelError(nameof(vm.Protocolo), "Protocolo inválido. Use o formato SOL-00012.");
                return View(vm);
            }

            var email = vm.Email.Trim().ToLowerInvariant();

            // Só devolve a solicitação quando o e-mail informado é o mesmo do pedido.
            // Isso impede que alguém veja dados de terceiros apenas trocando o número do protocolo.
            // AsNoTracking: consulta somente leitura, nada é alterado no banco.
            vm.Solicitacao = await _context.ServiceRequests
                .AsNoTracking()
                .Include(s => s.Service)
                .FirstOrDefaultAsync(s => s.Id == id && s.ContactEmail.ToLower() == email);

            return View(vm);
        }

        // =========================================================================================
        // MÉTODOS AUXILIARES (privados)
        // =========================================================================================

        // Valida e grava a foto do equipamento em wwwroot/uploads/requests.
        // Devolve o caminho público (ex.: /uploads/requests/abc.jpg) ou null se o arquivo não servir.
        private async Task<string?> SalvarFotoAsync(IFormFile foto)
        {
            // 1) Tamanho
            if (foto.Length > TamanhoMaximoFoto)
            {
                ModelState.AddModelError(nameof(ServiceRequestFormViewModel.Photo),
                    "A foto pode ter no máximo 5 MB.");
                return null;
            }

            // 2) Extensão: barra arquivos executáveis (.exe, .php...) com nome disfarçado
            var extensao = Path.GetExtension(foto.FileName).ToLowerInvariant();
            if (!ExtensoesFotoPermitidas.Contains(extensao))
            {
                ModelState.AddModelError(nameof(ServiceRequestFormViewModel.Photo),
                    "Envie uma imagem JPG, PNG ou WEBP.");
                return null;
            }

            // 3) Conteúdo: confere os primeiros bytes. É o que garante que é mesmo uma imagem,
            //    e não um programa renomeado para .jpg.
            await using var leitura = foto.OpenReadStream();
            var cabecalho = new byte[12];
            var lidos = await leitura.ReadAsync(cabecalho.AsMemory(0, cabecalho.Length));

            var ehJpg = lidos >= 3 && cabecalho[0] == 0xFF && cabecalho[1] == 0xD8 && cabecalho[2] == 0xFF;
            var ehPng = lidos >= 8 && cabecalho[0] == 0x89 && cabecalho[1] == 0x50 &&
                        cabecalho[2] == 0x4E && cabecalho[3] == 0x47;
            var ehWebp = lidos >= 12 && cabecalho[0] == 0x52 && cabecalho[1] == 0x49 &&
                         cabecalho[2] == 0x46 && cabecalho[3] == 0x46 &&
                         cabecalho[8] == 0x57 && cabecalho[9] == 0x45 &&
                         cabecalho[10] == 0x42 && cabecalho[11] == 0x50;

            if (!(ehJpg || ehPng || ehWebp))
            {
                ModelState.AddModelError(nameof(ServiceRequestFormViewModel.Photo),
                    "O arquivo enviado não parece ser uma imagem válida.");
                return null;
            }

            // 4) Gravação com nome aleatório (GUID): evita nomes repetidos e impede que alguém
            //    adivinhe o endereço da foto de outra pessoa.
            var pasta = Path.Combine(_ambiente.WebRootPath, "uploads", "requests");
            Directory.CreateDirectory(pasta);

            var nomeArquivo = $"{Guid.NewGuid():N}{extensao}";
            var caminhoCompleto = Path.Combine(pasta, nomeArquivo);

            // Volta o ponteiro ao início antes de copiar o arquivo inteiro.
            leitura.Position = 0;

            await using (var destino = System.IO.File.Create(caminhoCompleto))
            {
                await leitura.CopyToAsync(destino);
            }

            return $"/uploads/requests/{nomeArquivo}";
        }

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
