// ---------- Importações: trazem para este arquivo as classes que vamos usar ----------
using AnafixtechInformaticaSite.Data;                  // AppDbContext (acesso ao banco)
using AnafixtechInformaticaSite.Models;                // entidades (ServiceOrder, FinancialEntry...) e enums
using AnafixtechInformaticaSite.Models.ViewModels;     // view models das telas
using AnafixtechInformaticaSite.Services;              // Clock (hora de Brasília) e MoneyParser (texto → dinheiro)
using Microsoft.AspNetCore.Authorization;              // [Authorize]: exige login/permissão
using Microsoft.AspNetCore.Mvc;                        // Controller, IActionResult, [HttpPost]...
using Microsoft.AspNetCore.Mvc.Filters;                // ActionExecutingContext (usado no OnActionExecutionAsync)
using Microsoft.EntityFrameworkCore;                   // métodos assíncronos do banco: ToListAsync, Include...

namespace AnafixtechInformaticaSite.Controllers
{
    // ============================================================================================
    // DashboardController — painel administrativo da AnaFixTech (espelha o painel do Base44).
    // Um ÚNICO controller com uma "aba" por grupo de actions:
    //   Visão geral · Pedidos (OS) · Agenda · Estoque · Financeiro · Tickets · Notificações
    // ============================================================================================
    [Authorize(Policy = "Admin")]   // só entra quem está logado com o papel "Admin" (policy criada no Program.cs)
    [AutoValidateAntiforgeryToken]  // todo POST exige o token anti-falsificação (os formulários já o enviam)
    public class DashboardController : Controller
    {
        // Máximo de linhas mostradas nas tabelas grandes (evita páginas gigantes).
        private const int LimiteLinhas = 300;

        // Acesso ao banco de dados, recebido por injeção de dependência.
        private readonly AppDbContext _context;

        // Construtor: o ASP.NET entrega o AppDbContext pronto; guardamos para usar nas actions.
        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        // Roda ANTES de qualquer action deste controller.
        // Calcula quantas notificações não lidas existem para mostrar o número no sino do menu.
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Conta as notificações com IsRead = false e deixa o número disponível para as views.
            ViewBag.NaoLidas = await _context.Notifications.CountAsync(n => !n.IsRead);

            // Continua o fluxo normal: executa a action que foi pedida.
            await next();
        }

        // =========================================================================================
        // ABA: VISÃO GERAL  →  /Dashboard  ou  /Dashboard/Index
        // =========================================================================================
        public async Task<IActionResult> Index()
        {
            var hoje = Clock.Today;                                   // data de hoje (Brasília)
            var agora = Clock.Now;                                    // data e hora de agora
            var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1); // dia 1º do mês atual

            var vm = new DashboardIndexViewModel(); // objeto que levaremos para a view

            // Conta quantas OS existem em cada status (uma consulta só, agrupada no banco).
            var porStatus = await _context.ServiceOrders
                .GroupBy(o => o.Status)                                         // agrupa pelo status
                .Select(g => new { Status = g.Key, Total = g.Count() })         // para cada grupo: status e quantidade
                .ToListAsync();                                                 // executa a consulta
            vm.PedidosPorStatus = porStatus.ToDictionary(x => x.Status, x => x.Total); // vira dicionário Status → quantidade

            // Indicadores de pedidos, lidos do dicionário (GetValueOrDefault devolve 0 se o status não existir).
            vm.PedidosNovos = vm.PedidosPorStatus.GetValueOrDefault(OrderStatus.Pending);
            vm.PedidosEmAndamento = vm.PedidosPorStatus.GetValueOrDefault(OrderStatus.Waiting)
                                  + vm.PedidosPorStatus.GetValueOrDefault(OrderStatus.InProgress);

            // Soma das ENTRADAS do mês. O "(decimal?)" permite somar uma lista vazia sem erro (vira 0 com "?? 0m").
            vm.EntradasMes = await _context.FinancialEntries
                .Where(f => f.Tipo == EntryType.Entrada && f.Data >= inicioDoMes)
                .SumAsync(f => (decimal?)f.Valor) ?? 0m;

            // Soma das SAÍDAS do mês.
            vm.SaidasMes = await _context.FinancialEntries
                .Where(f => f.Tipo == EntryType.Saida && f.Data >= inicioDoMes)
                .SumAsync(f => (decimal?)f.Valor) ?? 0m;

            // "A receber": OS não canceladas e ainda não pagas por completo.
            var emAberto = await _context.ServiceOrders.AsNoTracking()
                .Where(o => o.Status != OrderStatus.Cancelled && o.PaymentStatus != OrderPaymentStatus.Paid)
                .ToListAsync();
            // Para cada OS: (preço final, senão estimado, senão 0) menos o que já foi recebido; nunca negativo.
            vm.AReceber = emAberto.Sum(o => Math.Max(0m, (o.FinalPrice ?? o.EstimatedPrice ?? 0m) - o.ValorRecebido));

            // Quantas OS foram entregues este mês (usa a data da última alteração como referência).
            var inicioDoMesComHora = inicioDoMes.ToDateTime(TimeOnly.MinValue);
            vm.EntregasNoMes = await _context.ServiceOrders
                .CountAsync(o => o.Status == OrderStatus.Delivered && o.UpdatedAt >= inicioDoMesComHora);

            // Tickets que ainda não foram resolvidos.
            vm.TicketsAbertos = await _context.SupportTickets.CountAsync(t => t.Status != TicketStatus.Resolved);

            // Listas curtas dos cartões da página.
            vm.UltimosPedidos = await _context.ServiceOrders.AsNoTracking()
                .OrderByDescending(o => o.CreatedAt).Take(6).ToListAsync();           // 6 OS mais recentes

            vm.ProximosAgendamentos = await _context.Agendamentos.AsNoTracking()
                .Where(a => a.Status == AgendamentoStatus.Agendado && a.DataHora >= agora)
                .OrderBy(a => a.DataHora).Take(5).ToListAsync();                      // 5 próximos compromissos

            vm.EstoqueBaixo = await _context.StockItems.AsNoTracking()
                .Where(s => s.Quantity <= s.MinQuantity)
                .OrderBy(s => s.Quantity).Take(6).ToListAsync();                      // peças no mínimo ou abaixo

            vm.TicketsEmAberto = await _context.SupportTickets.AsNoTracking()
                .Where(t => t.Status != TicketStatus.Resolved)
                // Prioridade é gravada como TEXTO ("High", "Medium", "Low"); ordenar pelo texto sairia em ordem alfabética.
                // Por isso traduzimos cada prioridade para um número: Alta = 0, Média = 1, Baixa = 2.
                .OrderBy(t => t.Priority == TicketPriority.High ? 0 : t.Priority == TicketPriority.Medium ? 1 : 2)
                .ThenBy(t => t.CreatedAt)                                             // dentro da mesma prioridade, o mais antigo primeiro
                .Take(5).ToListAsync();                                               // tickets abertos

            vm.NotificacoesRecentes = await _context.Notifications.AsNoTracking()
                .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync();           // últimas notificações

            return View(vm); // entrega tudo para Views/Dashboard/Index.cshtml
        }

        // =========================================================================================
        // ABA: PEDIDOS (OS)
        // =========================================================================================

        // Lista de OS com filtro por status e busca por texto.  →  /Dashboard/Pedidos?status=Pending&busca=dell
        public async Task<IActionResult> Pedidos(OrderStatus? status, string? busca)
        {
            // Começa com todas as OS (somente leitura) já trazendo o serviço do catálogo.
            var consulta = _context.ServiceOrders.AsNoTracking().Include(o => o.Service).AsQueryable();

            // Se escolheu um status, mantém só as OS desse status.
            if (status.HasValue)
                consulta = consulta.Where(o => o.Status == status.Value);

            // Se digitou algo na busca, procura em nome, equipamento, telefone e e-mail.
            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim(); // remove espaços das pontas
                consulta = consulta.Where(o => o.ClientName.Contains(termo) || o.ProductName.Contains(termo)
                                            || o.ClientPhone.Contains(termo) || o.ClientEmail.Contains(termo));
            }

            // Quantidade de OS por status (para os números nos botões de filtro).
            var contagem = await _context.ServiceOrders
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Total = g.Count() })
                .ToListAsync();

            var vm = new PedidosViewModel
            {
                Status = status,   // lembra o filtro escolhido para destacar o botão
                Busca = busca,     // lembra o texto digitado para repor no campo
                ContagemPorStatus = contagem.ToDictionary(x => x.Status, x => x.Total),
                // Mais recentes primeiro, limitado a 200 linhas.
                Pedidos = await consulta.OrderByDescending(o => o.CreatedAt).Take(200).ToListAsync()
            };
            return View(vm);
        }

        // Mostra o formulário de nova OS.  →  GET /Dashboard/PedidoNovo
        [HttpGet]
        public async Task<IActionResult> PedidoNovo()
        {
            var vm = new PedidoFormViewModel();   // formulário vazio
            await CarregarServicosAsync(vm);      // preenche a lista do <select> de serviços
            return View(vm);
        }

        // Recebe o formulário de nova OS.  →  POST /Dashboard/PedidoNovo
        [HttpPost]
        public async Task<IActionResult> PedidoNovo(PedidoFormViewModel vm)
        {
            // Converte o preço digitado (texto) em número, se ele foi preenchido.
            decimal? preco = null;
            if (!string.IsNullOrWhiteSpace(vm.EstimatedPrice))
            {
                if (MoneyParser.TryParse(vm.EstimatedPrice, out var valorDigitado))
                    preco = valorDigitado; // conversão ok
                else
                    ModelState.AddModelError(nameof(vm.EstimatedPrice), "Valor inválido. Use o formato 120,50.");
            }

            // Se escolheu um serviço do catálogo, confirma que ele existe.
            Service? servico = null;
            if (vm.ServiceId.HasValue)
            {
                servico = await _context.Services.FindAsync(vm.ServiceId.Value);
                if (servico == null)
                    ModelState.AddModelError(nameof(vm.ServiceId), "O serviço escolhido não existe mais.");
            }

            // Há erros de validação? Volta ao formulário mostrando as mensagens.
            if (!ModelState.IsValid)
            {
                await CarregarServicosAsync(vm); // a lista do <select> precisa ser recarregada
                return View(vm);
            }

            // Sem preço digitado, usa o preço do serviço do catálogo (se houver).
            if (preco == null && servico != null) preco = servico.Price;

            var agora = Clock.Now;

            // Monta a nova OS com os dados do formulário.
            var pedido = new ServiceOrder
            {
                ClientName = vm.ClientName.Trim(),
                ClientEmail = vm.ClientEmail.Trim().ToLowerInvariant(),
                ClientPhone = vm.ClientPhone.Trim(),
                ClientAddress = string.IsNullOrWhiteSpace(vm.ClientAddress) ? null : vm.ClientAddress.Trim(),
                PickupRequested = vm.PickupRequested,
                ProductName = vm.ProductName.Trim(),
                ProblemDescription = vm.ProblemDescription.Trim(),
                Urgency = vm.Urgency,
                ServiceId = servico?.Id,                              // null quando não escolheu serviço
                EstimatedPrice = preco,
                PriceNote = preco == null ? "A combinar" : null,      // sem preço: anota "A combinar"
                Status = OrderStatus.Pending,
                PaymentStatus = OrderPaymentStatus.Pending,
                CreatedAt = agora,
                UpdatedAt = agora
            };

            // Primeira linha do histórico: criação da OS.
            pedido.StatusHistory.Add(new StatusHistoryEntry
            {
                FromStatus = null,                  // não havia status antes
                ToStatus = OrderStatus.Pending,
                ChangedAt = agora,
                ChangedBy = NomeDoUsuario(),
                Note = "OS criada pelo painel"
            });

            _context.ServiceOrders.Add(pedido); // marca a OS para ser inserida
            await _context.SaveChangesAsync();  // grava no banco (agora pedido.Id existe)

            // Avisa no sino (agora que já temos o Id da OS). Urgente vira alerta vermelho.
            Notificar(pedido.Urgency == UrgencyLevel.Urgent ? NotificationType.Urgent : NotificationType.OsNew,
                      $"Nova OS {pedido.Codigo}", $"{pedido.ClientName} — {pedido.ProductName}", pedido.Id, "order");
            await _context.SaveChangesAsync();

            Mensagem(true, $"OS {pedido.Codigo} criada.");
            return RedirectToAction(nameof(PedidoDetalhes), new { id = pedido.Id }); // abre a OS recém-criada
        }

        // Detalhes de uma OS.  →  /Dashboard/PedidoDetalhes/12
        public async Task<IActionResult> PedidoDetalhes(int id)
        {
            // Busca a OS já trazendo o serviço, a peça usada e o histórico de status.
            var pedido = await _context.ServiceOrders
                .Include(o => o.Service)
                .Include(o => o.StockItem)
                .Include(o => o.StatusHistory)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (pedido == null) return NotFound(); // OS inexistente: erro 404

            var vm = new PedidoDetalhesViewModel
            {
                Pedido = pedido,
                // Pagamentos já registrados nesta OS, do mais recente para o mais antigo.
                Lancamentos = await _context.FinancialEntries.AsNoTracking()
                    .Where(f => f.ServiceOrderId == id)
                    .OrderByDescending(f => f.Data).ToListAsync(),
                // Lista de peças para o campo "peça usada".
                Pecas = await _context.StockItems.AsNoTracking().OrderBy(s => s.Name).ToListAsync()
            };
            return View(vm);
        }

        // Muda o status da OS e registra no histórico.  →  POST /Dashboard/PedidoStatus
        [HttpPost]
        public async Task<IActionResult> PedidoStatus(int id, OrderStatus status, string? nota)
        {
            var pedido = await _context.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (pedido == null) return NotFound();

            // Nada a fazer se o status escolhido é o mesmo que já está.
            if (pedido.Status == status)
            {
                Mensagem(false, "A OS já está nesse status.");
                return RedirectToAction(nameof(PedidoDetalhes), new { id });
            }

            // TRAVA contra o bug antigo do Base44 (OS entregues virando "canceladas" em lote):
            // cancelar uma OS já concluída/entregue exige escrever o motivo.
            var estavaFinalizada = pedido.Status == OrderStatus.Completed || pedido.Status == OrderStatus.Delivered;
            if (estavaFinalizada && status == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(nota))
            {
                Mensagem(false, "Para cancelar uma OS já concluída ou entregue, informe o motivo.");
                return RedirectToAction(nameof(PedidoDetalhes), new { id });
            }

            var anterior = pedido.Status; // guarda o status antigo para o histórico
            pedido.Status = status;       // aplica o novo status
            pedido.UpdatedAt = Clock.Now; // atualiza a data de alteração

            // Registra a mudança no histórico (quem, quando, de onde para onde e por quê).
            _context.StatusHistory.Add(new StatusHistoryEntry
            {
                ServiceOrderId = pedido.Id,
                FromStatus = anterior,
                ToStatus = status,
                ChangedAt = Clock.Now,
                ChangedBy = NomeDoUsuario(),
                Note = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim()
            });

            await _context.SaveChangesAsync();
            Mensagem(true, $"Status atualizado para “{status.ToLabel()}”.");
            return RedirectToAction(nameof(PedidoDetalhes), new { id });
        }

        // Salva dados técnicos e valores da OS.  →  POST /Dashboard/PedidoSalvar
        [HttpPost]
        public async Task<IActionResult> PedidoSalvar(int id, UrgencyLevel urgency, string? technicianNotes,
            string? estimatedPrice, string? finalPrice, string? priceNote, DateOnly? deliveryDate, int? stockItemId)
        {
            var pedido = await _context.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (pedido == null) return NotFound();

            // Converte os dois campos de dinheiro; vazio vira null, inválido interrompe.
            if (!TentarLerValorOpcional(estimatedPrice, out var estimado) || !TentarLerValorOpcional(finalPrice, out var final))
            {
                Mensagem(false, "Valor inválido. Use o formato 120,50.");
                return RedirectToAction(nameof(PedidoDetalhes), new { id });
            }

            // Copia os campos recebidos para a OS.
            pedido.Urgency = urgency;
            pedido.TechnicianNotes = string.IsNullOrWhiteSpace(technicianNotes) ? null : technicianNotes.Trim();
            pedido.EstimatedPrice = estimado;
            pedido.FinalPrice = final;
            pedido.PriceNote = string.IsNullOrWhiteSpace(priceNote) ? null : priceNote.Trim();
            pedido.DeliveryDate = deliveryDate;
            pedido.StockItemId = stockItemId;
            pedido.UpdatedAt = Clock.Now;

            // Se o valor combinado mudou, a situação do pagamento pode mudar também.
            var devido = pedido.FinalPrice ?? pedido.EstimatedPrice;
            if (devido.HasValue)
                pedido.PaymentStatus = CalcularPagamento(devido.Value, pedido.ValorRecebido);

            await _context.SaveChangesAsync();
            Mensagem(true, "Dados da OS salvos.");
            return RedirectToAction(nameof(PedidoDetalhes), new { id });
        }

        // Registra um pagamento: atualiza a OS E cria o lançamento financeiro, juntos (mesma transação).
        // Isso evita o problema "receita lançada, mas OS ainda como pendente".
        [HttpPost]
        public async Task<IActionResult> PedidoPagamento(int id, string valor, PaymentMethod forma, DateOnly? data, string? observacao)
        {
            // O valor precisa ser um número maior que zero.
            if (!MoneyParser.TryParse(valor, out var valorPago) || valorPago <= 0)
            {
                Mensagem(false, "Informe um valor válido maior que zero (ex.: 120,50).");
                return RedirectToAction(nameof(PedidoDetalhes), new { id });
            }

            var pedido = await _context.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (pedido == null) return NotFound();

            // Não se recebe pagamento de OS cancelada.
            if (pedido.Status == OrderStatus.Cancelled)
            {
                Mensagem(false, "Não é possível registrar pagamento em uma OS cancelada.");
                return RedirectToAction(nameof(PedidoDetalhes), new { id });
            }

            var dia = data ?? Clock.Today;                       // usa hoje se não informou a data
            var recebido = pedido.ValorRecebido + valorPago;     // total recebido depois deste pagamento

            // Se a OS não tinha preço definido, o pagamento passa a ser o valor final.
            if (pedido.FinalPrice == null && pedido.EstimatedPrice == null)
                pedido.FinalPrice = recebido;

            var devido = pedido.FinalPrice ?? pedido.EstimatedPrice ?? recebido; // quanto a OS vale

            // Atualiza os campos de pagamento da OS.
            pedido.ValorRecebido = recebido;
            pedido.PaymentMethod = forma;
            pedido.PaymentDate = dia;
            pedido.PaymentStatus = CalcularPagamento(devido, recebido);
            pedido.UpdatedAt = Clock.Now;

            // Cria o lançamento de ENTRADA ligado à OS.
            _context.FinancialEntries.Add(new FinancialEntry
            {
                Data = dia,
                Tipo = EntryType.Entrada,
                Categoria = "Servico de Manutencao",
                Descricao = $"Pagamento {pedido.Codigo} — {pedido.ProductName}",
                Valor = valorPago,
                PagadorRecebedor = pedido.ClientName,
                FormaPagamento = forma,
                ServicoPeca = pedido.ProductName,
                Observacoes = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
                Origem = EntryOrigin.OS,
                Socio = "Empresa",
                NumeroOs = pedido.Codigo,
                ServiceOrderId = pedido.Id,
                CreatedAt = Clock.Now
            });

            // Notificação de pagamento recebido.
            Notificar(NotificationType.Payment, "Pagamento recebido",
                      $"{pedido.Codigo}: {Fmt.Money(valorPago)} via {forma.ToLabel()}", pedido.Id, "order");

            await _context.SaveChangesAsync(); // grava OS + lançamento + notificação de uma vez
            Mensagem(true, $"Pagamento de {Fmt.Money(valorPago)} registrado na OS e no financeiro.");
            return RedirectToAction(nameof(PedidoDetalhes), new { id });
        }

        // Exclui uma OS (somente se ela ainda não tiver pagamentos).  →  POST /Dashboard/PedidoExcluir
        [HttpPost]
        public async Task<IActionResult> PedidoExcluir(int id)
        {
            var pedido = await _context.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (pedido == null) return NotFound();

            // Se já existe pagamento, a OS não pode sumir (o financeiro ficaria sem origem).
            if (await _context.FinancialEntries.AnyAsync(f => f.ServiceOrderId == id))
            {
                Mensagem(false, "Esta OS tem pagamentos registrados. Cancele-a em vez de excluir.");
                return RedirectToAction(nameof(PedidoDetalhes), new { id });
            }

            // Desliga os agendamentos que apontavam para esta OS (eles continuam existindo).
            var agendamentos = await _context.Agendamentos.Where(a => a.ServiceOrderId == id).ToListAsync();
            foreach (var a in agendamentos) a.ServiceOrderId = null;

            _context.ServiceOrders.Remove(pedido); // o histórico de status é apagado junto (cascata)
            await _context.SaveChangesAsync();

            Mensagem(true, $"OS {pedido.Codigo} excluída.");
            return RedirectToAction(nameof(Pedidos));
        }

        // =========================================================================================
        // ABA: FINANCEIRO
        // =========================================================================================

        // Resumo e lista de lançamentos do período.  →  /Dashboard/Financeiro?de=2026-10-01&ate=2026-10-31
        public async Task<IActionResult> Financeiro(DateOnly? de, DateOnly? ate, EntryType? tipo, string? socio)
        {
            var hoje = Clock.Today;
            var inicio = de ?? new DateOnly(hoje.Year, hoje.Month, 1); // padrão: do dia 1º do mês...
            var fim = ate ?? hoje;                                      // ...até hoje
            if (fim < inicio) (inicio, fim) = (fim, inicio);            // se vieram trocadas, inverte

            // Todos os lançamentos do período (qualquer sócio).
            var doPeriodo = _context.FinancialEntries.AsNoTracking()
                .Where(f => f.Data >= inicio && f.Data <= fim);

            // O mesmo conjunto, filtrado por sócio quando o usuário escolheu um.
            var filtrado = doPeriodo;
            if (!string.IsNullOrWhiteSpace(socio))
                filtrado = filtrado.Where(f => f.Socio == socio);

            var vm = new FinanceiroViewModel { De = inicio, Ate = fim, Tipo = tipo, Socio = socio };

            // Totais de entradas e saídas (respeitam o filtro de sócio).
            vm.Entradas = await filtrado.Where(f => f.Tipo == EntryType.Entrada).SumAsync(f => (decimal?)f.Valor) ?? 0m;
            vm.Saidas = await filtrado.Where(f => f.Tipo == EntryType.Saida).SumAsync(f => (decimal?)f.Valor) ?? 0m;

            // Resumo por categoria: agrupa, ordena pelo maior total e transforma em CategoriaTotal.
            vm.PorCategoria = await filtrado
                .GroupBy(f => new { f.Tipo, f.Categoria })
                .OrderByDescending(g => g.Sum(x => x.Valor))
                .Select(g => new CategoriaTotal { Tipo = g.Key.Tipo, Categoria = g.Key.Categoria, Total = g.Sum(x => x.Valor) })
                .ToListAsync();

            // Resumo por sócio: usa o período inteiro (sem filtro de sócio) para comparar todos.
            vm.PorSocio = await doPeriodo
                .GroupBy(f => f.Socio)
                .Select(g => new SocioTotal
                {
                    Socio = g.Key,
                    Entradas = g.Where(x => x.Tipo == EntryType.Entrada).Sum(x => x.Valor),
                    Saidas = g.Where(x => x.Tipo == EntryType.Saida).Sum(x => x.Valor)
                })
                .ToListAsync();

            // Linhas da tabela: aplica também o filtro de tipo, mais recentes primeiro.
            var linhas = filtrado;
            if (tipo.HasValue)
                linhas = linhas.Where(f => f.Tipo == tipo.Value);

            var lista = await linhas.Include(f => f.ServiceOrder)
                .OrderByDescending(f => f.Data).ThenByDescending(f => f.Id)
                .Take(LimiteLinhas + 1) // pega uma a mais só para saber se havia mais linhas que o limite
                .ToListAsync();

            vm.Cortado = lista.Count > LimiteLinhas;            // true se passou do limite
            vm.Lancamentos = lista.Take(LimiteLinhas).ToList(); // mostra só até o limite
            return View(vm);
        }

        // Formulário de novo lançamento.  →  GET /Dashboard/LancamentoNovo
        [HttpGet]
        public IActionResult LancamentoNovo(EntryType tipo = EntryType.Entrada)
        {
            return View(new LancamentoFormViewModel { Data = Clock.Today, Tipo = tipo }); // já vem com a data de hoje
        }

        // Grava o novo lançamento.  →  POST /Dashboard/LancamentoNovo
        [HttpPost]
        public async Task<IActionResult> LancamentoNovo(LancamentoFormViewModel vm)
        {
            // O valor digitado precisa ser um número maior que zero.
            if (!MoneyParser.TryParse(vm.Valor, out var valor) || valor <= 0)
                ModelState.AddModelError(nameof(vm.Valor), "Valor inválido. Use o formato 120,50.");

            if (!ModelState.IsValid) return View(vm); // erros: volta ao formulário

            _context.FinancialEntries.Add(new FinancialEntry
            {
                Data = vm.Data!.Value,                      // "!" porque a validação [Required] garante que existe
                Tipo = vm.Tipo,
                Categoria = vm.Categoria.Trim(),
                Descricao = vm.Descricao.Trim(),
                Valor = valor,
                PagadorRecebedor = string.IsNullOrWhiteSpace(vm.PagadorRecebedor) ? null : vm.PagadorRecebedor.Trim(),
                FormaPagamento = vm.FormaPagamento,
                ServicoPeca = string.IsNullOrWhiteSpace(vm.ServicoPeca) ? null : vm.ServicoPeca.Trim(),
                Observacoes = string.IsNullOrWhiteSpace(vm.Observacoes) ? null : vm.Observacoes.Trim(),
                Origem = EntryOrigin.Manual,                // digitado à mão
                Socio = vm.Socio,
                CreatedAt = Clock.Now
            });
            await _context.SaveChangesAsync();

            Mensagem(true, "Lançamento registrado.");
            return RedirectToAction(nameof(Financeiro));
        }

        // Exclui um lançamento.  →  POST /Dashboard/LancamentoExcluir
        [HttpPost]
        public async Task<IActionResult> LancamentoExcluir(int id)
        {
            var lancamento = await _context.FinancialEntries.FirstOrDefaultAsync(f => f.Id == id);
            if (lancamento == null) return NotFound();

            // Se o lançamento era um pagamento de OS, devolve esse valor à OS para os dois lados continuarem batendo.
            if (lancamento.Origem == EntryOrigin.OS && lancamento.ServiceOrderId.HasValue)
            {
                var pedido = await _context.ServiceOrders.FirstOrDefaultAsync(o => o.Id == lancamento.ServiceOrderId.Value);
                if (pedido != null)
                {
                    pedido.ValorRecebido = Math.Max(0m, pedido.ValorRecebido - lancamento.Valor); // nunca fica negativo
                    var devido = pedido.FinalPrice ?? pedido.EstimatedPrice;
                    if (devido.HasValue)
                        pedido.PaymentStatus = CalcularPagamento(devido.Value, pedido.ValorRecebido);
                    pedido.UpdatedAt = Clock.Now;
                }
            }

            _context.FinancialEntries.Remove(lancamento);
            await _context.SaveChangesAsync();

            Mensagem(true, "Lançamento excluído.");
            return RedirectToAction(nameof(Financeiro));
        }

        // =========================================================================================
        // ABA: ESTOQUE
        // =========================================================================================

        // Lista de peças.  →  /Dashboard/Estoque?somenteBaixo=true&busca=ssd
        public async Task<IActionResult> Estoque(bool somenteBaixo = false, string? busca = null)
        {
            var consulta = _context.StockItems.AsNoTracking().AsQueryable();

            if (somenteBaixo)
                consulta = consulta.Where(s => s.Quantity <= s.MinQuantity); // só as que estão no mínimo ou abaixo

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim();
                consulta = consulta.Where(s => s.Name.Contains(termo) || (s.Supplier != null && s.Supplier.Contains(termo)));
            }

            var vm = new EstoqueViewModel
            {
                SomenteBaixo = somenteBaixo,
                Busca = busca,
                // As mais críticas primeiro (menor quantidade), depois por nome.
                Itens = await consulta.OrderBy(s => s.Quantity).ThenBy(s => s.Name).ToListAsync()
            };
            return View(vm);
        }

        // Cadastra uma peça nova.  →  POST /Dashboard/EstoqueNovo
        [HttpPost]
        public async Task<IActionResult> EstoqueNovo(EstoqueFormViewModel vm)
        {
            // Preço de compra é opcional; se vier preenchido precisa ser válido.
            decimal? preco = null;
            if (!string.IsNullOrWhiteSpace(vm.PurchasePrice))
            {
                if (MoneyParser.TryParse(vm.PurchasePrice, out var p)) preco = p;
                else ModelState.AddModelError(nameof(vm.PurchasePrice), "Preço inválido. Use o formato 120,50.");
            }

            // Erros: como o formulário fica na própria página de estoque, volta com a mensagem.
            if (!ModelState.IsValid)
            {
                Mensagem(false, ErrosDoFormulario());
                return RedirectToAction(nameof(Estoque));
            }

            _context.StockItems.Add(new StockItem
            {
                Name = vm.Name.Trim(),
                Category = vm.Category,
                Quantity = vm.Quantity,
                MinQuantity = vm.MinQuantity,
                PurchasePrice = preco,
                Supplier = string.IsNullOrWhiteSpace(vm.Supplier) ? null : vm.Supplier.Trim(),
                Location = string.IsNullOrWhiteSpace(vm.Location) ? null : vm.Location.Trim(),
                Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim()
            });
            await _context.SaveChangesAsync();

            Mensagem(true, "Peça cadastrada.");
            return RedirectToAction(nameof(Estoque));
        }

        // Registra a COMPRA de peças: aumenta o estoque e lança a saída no financeiro.  →  POST /Dashboard/EstoqueComprar
        [HttpPost]
        public async Task<IActionResult> EstoqueComprar(int id, int quantidade, string valorUnitario, PaymentMethod forma, string? fornecedor)
        {
            // Quantidade e valor unitário precisam ser positivos.
            if (quantidade <= 0 || !MoneyParser.TryParse(valorUnitario, out var unitario) || unitario <= 0)
            {
                Mensagem(false, "Informe uma quantidade e um valor unitário válidos.");
                return RedirectToAction(nameof(Estoque));
            }

            var item = await _context.StockItems.FirstOrDefaultAsync(s => s.Id == id);
            if (item == null) return NotFound();

            var hoje = Clock.Today;
            var fornecedorFinal = string.IsNullOrWhiteSpace(fornecedor) ? item.Supplier : fornecedor.Trim();

            // Atualiza a peça: soma a quantidade e guarda preço, data e fornecedor da compra.
            item.Quantity += quantidade;
            item.PurchasePrice = unitario;
            item.LastPurchaseDate = hoje;
            item.Supplier = fornecedorFinal;

            // Lança a SAÍDA no financeiro (quantidade × valor unitário).
            _context.FinancialEntries.Add(new FinancialEntry
            {
                Data = hoje,
                Tipo = EntryType.Saida,
                Categoria = "Pecas e Componentes",
                Descricao = $"Compra: {quantidade}x {item.Name}",
                Valor = quantidade * unitario,
                PagadorRecebedor = fornecedorFinal,
                FormaPagamento = forma,
                ServicoPeca = item.Name,
                Origem = EntryOrigin.Estoque,
                Socio = "Empresa",
                StockItemId = item.Id,
                CreatedAt = Clock.Now
            });

            await _context.SaveChangesAsync();
            Mensagem(true, $"Compra registrada: +{quantidade} em estoque e saída de {Fmt.Money(quantidade * unitario)} no financeiro.");
            return RedirectToAction(nameof(Estoque));
        }

        // Ajusta a quantidade (positivo soma, negativo subtrai) sem mexer no financeiro.  →  POST /Dashboard/EstoqueAjustar
        [HttpPost]
        public async Task<IActionResult> EstoqueAjustar(int id, int delta)
        {
            var item = await _context.StockItems.FirstOrDefaultAsync(s => s.Id == id);
            if (item == null) return NotFound();

            var nivelAnterior = item.Level;                        // nível antes do ajuste
            item.Quantity = Math.Max(0, item.Quantity + delta);    // aplica o ajuste, sem deixar negativo
            var nivelNovo = item.Level;                            // nível depois do ajuste

            // Se o estoque ficou baixo/crítico agora, avisa no sino.
            if (nivelNovo != StockLevel.Ok && nivelNovo != nivelAnterior)
                Notificar(NotificationType.Stock, "Estoque baixo",
                          $"{item.Name}: restam {item.Quantity} (mínimo {item.MinQuantity})", item.Id, "stock");

            await _context.SaveChangesAsync();
            Mensagem(true, $"Quantidade de “{item.Name}” ajustada para {item.Quantity}.");
            return RedirectToAction(nameof(Estoque));
        }

        // Exclui uma peça.  →  POST /Dashboard/EstoqueExcluir
        [HttpPost]
        public async Task<IActionResult> EstoqueExcluir(int id)
        {
            var item = await _context.StockItems.FirstOrDefaultAsync(s => s.Id == id);
            if (item == null) return NotFound();

            // Desliga as OS e os lançamentos que citavam esta peça (eles continuam existindo).
            var pedidos = await _context.ServiceOrders.Where(o => o.StockItemId == id).ToListAsync();
            foreach (var o in pedidos) o.StockItemId = null;
            var lancamentos = await _context.FinancialEntries.Where(f => f.StockItemId == id).ToListAsync();
            foreach (var f in lancamentos) f.StockItemId = null;

            _context.StockItems.Remove(item);
            await _context.SaveChangesAsync();

            Mensagem(true, $"Peça “{item.Name}” excluída.");
            return RedirectToAction(nameof(Estoque));
        }

        // =========================================================================================
        // ABA: AGENDA
        // =========================================================================================

        // Compromissos e horários disponíveis.  →  /Dashboard/Agenda
        public async Task<IActionResult> Agenda()
        {
            var agora = Clock.Now;
            var desde = agora.AddDays(-7); // mostra também a última semana

            var vm = new AgendaViewModel
            {
                // Compromissos dos últimos 7 dias em diante, em ordem de data.
                Agendamentos = await _context.Agendamentos.AsNoTracking()
                    .Include(a => a.ServiceOrder)
                    .Where(a => a.DataHora >= desde)
                    .OrderBy(a => a.DataHora).Take(LimiteLinhas).ToListAsync(),

                // Horários que ainda não passaram.
                Horarios = await _context.HorariosDisponiveis.AsNoTracking()
                    .Where(h => h.DataHora >= agora)
                    .OrderBy(h => h.DataHora).Take(LimiteLinhas).ToListAsync(),

                // OS ainda em andamento, para escolher no formulário de novo agendamento.
                PedidosAbertos = await _context.ServiceOrders.AsNoTracking()
                    .Where(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled)
                    .OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync()
            };
            return View(vm);
        }

        // Cria um agendamento.  →  POST /Dashboard/AgendamentoNovo
        [HttpPost]
        public async Task<IActionResult> AgendamentoNovo(AgendamentoFormViewModel vm)
        {
            // Agendamento do tipo "OS" precisa apontar para uma OS.
            if (vm.Tipo == AgendamentoTipo.OS && !vm.ServiceOrderId.HasValue)
                ModelState.AddModelError(nameof(vm.ServiceOrderId), "Escolha a OS do agendamento.");

            if (!ModelState.IsValid)
            {
                Mensagem(false, ErrosDoFormulario());
                return RedirectToAction(nameof(Agenda));
            }

            var nome = vm.ClientName;   // nome do cliente (pode vir em branco)
            var telefone = vm.ClientPhone;

            // Se há uma OS escolhida e o nome/telefone não foram digitados, copia os dados dela.
            if (vm.ServiceOrderId.HasValue)
            {
                var pedido = await _context.ServiceOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == vm.ServiceOrderId.Value);
                if (pedido == null)
                {
                    Mensagem(false, "A OS escolhida não existe.");
                    return RedirectToAction(nameof(Agenda));
                }
                if (string.IsNullOrWhiteSpace(nome)) nome = pedido.ClientName;
                if (string.IsNullOrWhiteSpace(telefone)) telefone = pedido.ClientPhone;
            }

            _context.Agendamentos.Add(new Agendamento
            {
                Tipo = vm.Tipo,
                ServiceOrderId = vm.ServiceOrderId,
                Tecnico = vm.Tecnico.Trim(),
                DataHora = vm.DataHora!.Value,
                Descricao = vm.Descricao.Trim(),
                Endereco = string.IsNullOrWhiteSpace(vm.Endereco) ? null : vm.Endereco.Trim(),
                ClientName = string.IsNullOrWhiteSpace(nome) ? null : nome.Trim(),
                ClientPhone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim(),
                Status = AgendamentoStatus.Agendado,
                CreatedAt = Clock.Now
            });
            await _context.SaveChangesAsync();

            Mensagem(true, "Agendamento criado.");
            return RedirectToAction(nameof(Agenda));
        }

        // Muda o status de um agendamento (agendado / concluído / cancelado).  →  POST /Dashboard/AgendamentoAlterarStatus
        [HttpPost]
        public async Task<IActionResult> AgendamentoAlterarStatus(int id, AgendamentoStatus status)
        {
            var agendamento = await _context.Agendamentos.FirstOrDefaultAsync(a => a.Id == id);
            if (agendamento == null) return NotFound();

            agendamento.Status = status;
            await _context.SaveChangesAsync();

            Mensagem(true, $"Agendamento marcado como “{status.ToLabel()}”.");
            return RedirectToAction(nameof(Agenda));
        }

        // Cria um horário disponível.  →  POST /Dashboard/HorarioNovo
        [HttpPost]
        public async Task<IActionResult> HorarioNovo(HorarioFormViewModel vm)
        {
            // Não faz sentido abrir horário no passado.
            if (vm.DataHora.HasValue && vm.DataHora.Value < Clock.Now)
                ModelState.AddModelError(nameof(vm.DataHora), "Escolha uma data e hora futuras.");

            if (!ModelState.IsValid)
            {
                Mensagem(false, ErrosDoFormulario());
                return RedirectToAction(nameof(Agenda));
            }

            _context.HorariosDisponiveis.Add(new HorarioDisponivel
            {
                DataHora = vm.DataHora!.Value,
                Tecnico = vm.Tecnico.Trim(),
                DuracaoMinutos = vm.DuracaoMinutos,
                Status = SlotStatus.Disponivel,
                Observacao = string.IsNullOrWhiteSpace(vm.Observacao) ? null : vm.Observacao.Trim()
            });
            await _context.SaveChangesAsync();

            Mensagem(true, "Horário criado.");
            return RedirectToAction(nameof(Agenda));
        }

        // Bloqueia ou libera um horário (alterna).  →  POST /Dashboard/HorarioBloquear
        [HttpPost]
        public async Task<IActionResult> HorarioBloquear(int id)
        {
            var horario = await _context.HorariosDisponiveis.FirstOrDefaultAsync(h => h.Id == id);
            if (horario == null) return NotFound();

            // Horário reservado por um cliente não pode ser bloqueado.
            if (horario.Status == SlotStatus.Reservado)
            {
                Mensagem(false, "Horário reservado não pode ser bloqueado.");
                return RedirectToAction(nameof(Agenda));
            }

            // Alterna: se estava bloqueado vira disponível; se estava disponível vira bloqueado.
            horario.Status = horario.Status == SlotStatus.Bloqueado ? SlotStatus.Disponivel : SlotStatus.Bloqueado;
            await _context.SaveChangesAsync();

            Mensagem(true, $"Horário agora está “{horario.Status.ToLabel()}”.");
            return RedirectToAction(nameof(Agenda));
        }

        // Exclui um horário.  →  POST /Dashboard/HorarioExcluir
        [HttpPost]
        public async Task<IActionResult> HorarioExcluir(int id)
        {
            var horario = await _context.HorariosDisponiveis.FirstOrDefaultAsync(h => h.Id == id);
            if (horario == null) return NotFound();

            if (horario.Status == SlotStatus.Reservado)
            {
                Mensagem(false, "Horário reservado não pode ser excluído.");
                return RedirectToAction(nameof(Agenda));
            }

            _context.HorariosDisponiveis.Remove(horario);
            await _context.SaveChangesAsync();

            Mensagem(true, "Horário excluído.");
            return RedirectToAction(nameof(Agenda));
        }

        // =========================================================================================
        // ABA: TICKETS
        // =========================================================================================

        // Lista de tickets.  →  /Dashboard/Tickets?status=Pending
        public async Task<IActionResult> Tickets(TicketStatus? status)
        {
            var consulta = _context.SupportTickets.AsNoTracking().AsQueryable();

            if (status.HasValue)
                consulta = consulta.Where(t => t.Status == status.Value); // filtra pelo status escolhido

            var vm = new TicketsViewModel
            {
                Status = status,
                // Mais recentes primeiro.
                Tickets = await consulta.OrderByDescending(t => t.CreatedAt).Take(LimiteLinhas).ToListAsync()
            };
            return View(vm);
        }

        // Abre um ticket.  →  POST /Dashboard/TicketNovo
        [HttpPost]
        public async Task<IActionResult> TicketNovo(TicketFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                Mensagem(false, ErrosDoFormulario());
                return RedirectToAction(nameof(Tickets));
            }

            var ticket = new SupportTicket
            {
                ClientName = vm.ClientName.Trim(),
                ClientPhone = string.IsNullOrWhiteSpace(vm.ClientPhone) ? null : vm.ClientPhone.Trim(),
                Subject = vm.Subject.Trim(),
                Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
                Priority = vm.Priority,
                AssignedTo = string.IsNullOrWhiteSpace(vm.AssignedTo) ? null : vm.AssignedTo.Trim(),
                Status = TicketStatus.Pending,
                CreatedAt = Clock.Now
            };

            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync(); // grava para o ticket ganhar um Id

            // Avisa no sino; prioridade alta vira alerta urgente.
            Notificar(ticket.Priority == TicketPriority.High ? NotificationType.Urgent : NotificationType.Ticket,
                      "Novo ticket de suporte", $"{ticket.ClientName}: {ticket.Subject}", ticket.Id, "ticket");
            await _context.SaveChangesAsync();

            Mensagem(true, "Ticket aberto.");
            return RedirectToAction(nameof(Tickets));
        }

        // Atualiza status, responsável e histórico de contato de um ticket.  →  POST /Dashboard/TicketAlterarStatus
        [HttpPost]
        public async Task<IActionResult> TicketAlterarStatus(int id, TicketStatus status, string? assignedTo, string? nota)
        {
            var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();

            ticket.Status = status;
            ticket.AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? ticket.AssignedTo : assignedTo.Trim();
            // Ao resolver guarda a data; se for reaberto, apaga a data de resolução.
            ticket.ResolvedAt = status == TicketStatus.Resolved ? Clock.Now : null;

            // Se escreveu uma nota, acrescenta ao histórico com data, hora e autor.
            if (!string.IsNullOrWhiteSpace(nota))
            {
                var linha = $"[{Fmt.Stamp(Clock.Now)}] {NomeDoUsuario()}: {nota.Trim()}";
                ticket.CommunicationLog = string.IsNullOrEmpty(ticket.CommunicationLog)
                    ? linha
                    : ticket.CommunicationLog + "\n" + linha;
            }

            await _context.SaveChangesAsync();
            Mensagem(true, $"Ticket atualizado: “{status.ToLabel()}”.");
            return RedirectToAction(nameof(Tickets));
        }

        // =========================================================================================
        // ABA: NOTIFICAÇÕES
        // =========================================================================================

        // Lista de notificações.  →  /Dashboard/Notificacoes
        public async Task<IActionResult> Notificacoes()
        {
            var vm = new NotificacoesViewModel
            {
                Notificacoes = await _context.Notifications.AsNoTracking()
                    .OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync()
            };
            return View(vm);
        }

        // Marca a notificação como lida e leva para o item relacionado.  →  GET /Dashboard/NotificacaoAbrir/5
        [HttpGet]
        public async Task<IActionResult> NotificacaoAbrir(int id)
        {
            var notificacao = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id);
            if (notificacao == null) return NotFound();

            notificacao.IsRead = true;           // marca como lida
            await _context.SaveChangesAsync();

            // Decide para onde ir conforme o tipo do item relacionado.
            if (notificacao.RefType == "order" && notificacao.RefId.HasValue)
                return RedirectToAction(nameof(PedidoDetalhes), new { id = notificacao.RefId.Value });
            if (notificacao.RefType == "ticket")
                return RedirectToAction(nameof(Tickets));
            if (notificacao.RefType == "stock")
                return RedirectToAction(nameof(Estoque), new { somenteBaixo = true });

            return RedirectToAction(nameof(Notificacoes)); // sem destino específico: volta à lista
        }

        // Marca todas como lidas.  →  POST /Dashboard/NotificacoesLidas
        [HttpPost]
        public async Task<IActionResult> NotificacoesLidas()
        {
            var naoLidas = await _context.Notifications.Where(n => !n.IsRead).ToListAsync();
            foreach (var n in naoLidas) n.IsRead = true; // marca cada uma
            await _context.SaveChangesAsync();

            Mensagem(true, "Todas as notificações foram marcadas como lidas.");
            return RedirectToAction(nameof(Notificacoes));
        }

        // =========================================================================================
        // MÉTODOS AUXILIARES (privados: não viram páginas, só ajudam as actions acima)
        // =========================================================================================

        // Preenche a lista de serviços do catálogo usada no <select> do formulário de OS.
        private async Task CarregarServicosAsync(PedidoFormViewModel vm)
        {
            vm.Servicos = await _context.Services.AsNoTracking().OrderBy(s => s.Title).ToListAsync();
        }

        // Cria uma notificação no sino. Não grava sozinha: quem chama decide quando usar SaveChangesAsync.
        private void Notificar(NotificationType tipo, string titulo, string mensagem, int? refId, string? refTipo)
        {
            _context.Notifications.Add(new Notification
            {
                Type = tipo,
                Title = titulo,
                Message = mensagem,
                RefId = refId,
                RefType = refTipo,
                IsRead = false,
                CreatedAt = Clock.Now
            });
        }

        // Guarda uma mensagem que aparece UMA vez na próxima tela (faixa verde de sucesso ou vermelha de erro).
        private void Mensagem(bool sucesso, string texto)
        {
            TempData[sucesso ? "Ok" : "Erro"] = texto;
        }

        // Junta todas as mensagens de erro de validação do formulário em um único texto.
        private string ErrosDoFormulario()
        {
            return string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        }

        // Nome de quem está logado, para o histórico (usa "Admin" se não houver nome).
        private string NomeDoUsuario()
        {
            return User.Identity?.Name ?? "Admin";
        }

        // Situação do pagamento: "Pago" só quando já se recebeu tudo (ou mais); senão "Pendente".
        private static OrderPaymentStatus CalcularPagamento(decimal devido, decimal recebido)
        {
            return recebido > 0m && recebido >= devido ? OrderPaymentStatus.Paid : OrderPaymentStatus.Pending;
        }

        // Lê um valor em dinheiro OPCIONAL: vazio vira null (válido); texto inválido devolve false.
        private static bool TentarLerValorOpcional(string? texto, out decimal? valor)
        {
            valor = null;                                    // padrão: sem valor
            if (string.IsNullOrWhiteSpace(texto)) return true; // vazio é aceitável
            if (MoneyParser.TryParse(texto, out var convertido))
            {
                valor = convertido; // conversão ok
                return true;
            }
            return false; // texto que não é número
        }
    }
}
