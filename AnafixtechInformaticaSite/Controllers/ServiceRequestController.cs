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
            // imagem de um envio que será recusado.
            byte[]? dadosFoto = null;
            string? tipoFoto = null;
            if (vm.Photo is not null && vm.Photo.Length > 0)
            {
                (dadosFoto, tipoFoto) = await LerFotoAsync(vm.Photo);

                // O método devolve null quando o arquivo é inválido e já registrou o erro.
                if (dadosFoto is null)
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

                // Foto guardada no banco (não mais em disco): sobrevive aos redeploys do Render.
                PhotoData = dadosFoto,
                PhotoContentType = tipoFoto
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
        // FOTO DO EQUIPAMENTO (somente para a equipe)
        // =========================================================================================

        // Entrega a imagem guardada no banco.  →  GET /ServiceRequest/Foto/12
        // A classe já exige login de administrador ([Authorize(Policy = "Admin")]), porque a foto
        // pode mostrar dados do cliente e não deve ficar acessível a qualquer visitante.
        [HttpGet]
        public async Task<IActionResult> Foto(int id)
        {
            // Carrega só as duas colunas da imagem: evita trazer a solicitação inteira
            // (descrição, anotações internas etc.) apenas para exibir uma foto.
            var foto = await _context.ServiceRequests
                .AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => new { s.PhotoData, s.PhotoContentType })
                .FirstOrDefaultAsync();

            // Sem foto (ou registro inexistente): 404, sem imagem quebrada na tela.
            if (foto?.PhotoData is null || foto.PhotoData.Length == 0)
                return NotFound();

            // O tipo foi decidido pelos bytes no upload (image/jpeg, image/png ou image/webp).
            var tipo = string.IsNullOrWhiteSpace(foto.PhotoContentType)
                ? "application/octet-stream"
                : foto.PhotoContentType;

            // "private": o navegador do admin pode guardar por 5 minutos, mas nenhum cache
            // compartilhado (proxy/CDN) deve servir essa imagem para outra pessoa.
            Response.Headers.CacheControl = "private, max-age=300";

            return File(foto.PhotoData, tipo);
        }

        // =========================================================================================
        // MÉTODOS AUXILIARES (privados)
        // =========================================================================================

        // Valida a foto do equipamento e devolve os BYTES e o tipo real da imagem.
        // Devolve (null, null) quando o arquivo não serve, já registrando o erro na tela.
        //
        // Antes esta rotina gravava o arquivo em wwwroot/uploads/requests. Agora a imagem fica
        // na memória e é gravada na coluna PhotoData (bytea) do PostgreSQL, porque o disco do
        // Render free é temporário e apagava as fotos a cada deploy.
        private async Task<(byte[]? Dados, string? Tipo)> LerFotoAsync(IFormFile foto)
        {
            // 1) Tamanho
            if (foto.Length > TamanhoMaximoFoto)
            {
                ModelState.AddModelError(nameof(ServiceRequestFormViewModel.Photo),
                    "A foto pode ter no máximo 5 MB.");
                return (null, null);
            }

            // 2) Extensão: barra arquivos executáveis (.exe, .php...) com nome disfarçado
            var extensao = Path.GetExtension(foto.FileName).ToLowerInvariant();
            if (!ExtensoesFotoPermitidas.Contains(extensao))
            {
                ModelState.AddModelError(nameof(ServiceRequestFormViewModel.Photo),
                    "Envie uma imagem JPG, PNG ou WEBP.");
                return (null, null);
            }

            // 3) Lê o arquivo inteiro para a memória (no máximo 5 MB, conferido acima).
            using var memoria = new MemoryStream();
            await foto.CopyToAsync(memoria);
            var dados = memoria.ToArray();

            // 4) Conteúdo: confere os primeiros bytes. É o que garante que é mesmo uma imagem,
            //    e não um programa renomeado para .jpg.
            var ehJpg = dados.Length >= 3 && dados[0] == 0xFF && dados[1] == 0xD8 && dados[2] == 0xFF;
            var ehPng = dados.Length >= 8 && dados[0] == 0x89 && dados[1] == 0x50 &&
                        dados[2] == 0x4E && dados[3] == 0x47;
            var ehWebp = dados.Length >= 12 && dados[0] == 0x52 && dados[1] == 0x49 &&
                         dados[2] == 0x46 && dados[3] == 0x46 &&
                         dados[8] == 0x57 && dados[9] == 0x45 &&
                         dados[10] == 0x42 && dados[11] == 0x50;

            if (!(ehJpg || ehPng || ehWebp))
            {
                ModelState.AddModelError(nameof(ServiceRequestFormViewModel.Photo),
                    "O arquivo enviado não parece ser uma imagem válida.");
                return (null, null);
            }

            // O tipo é decidido pelos bytes, não pelo que o navegador informou no upload.
            var tipo = ehJpg ? "image/jpeg" : ehPng ? "image/png" : "image/webp";
            return (dados, tipo);
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
