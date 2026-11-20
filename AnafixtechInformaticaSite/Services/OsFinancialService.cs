using AnafixtechInformaticaSite.Data;                  // AppDbContext: ponte com o banco
using AnafixtechInformaticaSite.Models;                // entidades e enums
using Microsoft.EntityFrameworkCore;                   // Include, ToListAsync, SaveChangesAsync
using Microsoft.EntityFrameworkCore.Storage;           // IDbContextTransaction

namespace AnafixtechInformaticaSite.Services
{
    // Resultado simples devolvido pelas decisões (aprovar/ignorar).
    // "Conflito" quer dizer: outra aba já decidiu esta sugestão; não gravamos nada.
    public class ResultadoSugestao
    {
        public bool Sucesso { get; set; }
        public bool Conflito { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public string CodigoOs { get; set; } = string.Empty;
        public int OsId { get; set; }

        public static ResultadoSugestao Erro(string mensagem) => new() { Mensagem = mensagem };
    }

    // Ajustes opcionais do botão "Editar e aprovar". Tudo que vier nulo usa o sugerido.
    public class AjustesAprovacao
    {
        public decimal? Valor { get; set; }
        public DateOnly? Data { get; set; }
        public PaymentMethod? FormaPagamento { get; set; }
        public string? Socio { get; set; }
        public string? Observacao { get; set; }
    }

    // Um item do lote: par (sugestão, token) que veio do formulário. O token via junto
    // com o checkbox para que um formulário antigo não decida uma sugestão já mudada.
    public class ItemAprovacaoLote
    {
        public int Id { get; set; }
        public Guid Token { get; set; }
    }

    // Ajustes permitidos no lote: só data, forma e observação.
    // Valor e sócio ficam de fora de propósito: cada sugestão mantém os seus.
    public class AjustesLote
    {
        public DateOnly? Data { get; set; }
        public PaymentMethod? FormaPagamento { get; set; }
        public string? Observacao { get; set; }
    }

    // Resumo do lote. As mensagens são curtas e usam só o código da OS (nunca nomes).
    public class ResultadoLote
    {
        public int Aprovados { get; set; }
        public int Conflitos { get; set; }
        public int Erros { get; set; }
        public decimal ValorTotal { get; set; } // soma só dos que foram aprovados
        public List<string> Atencoes { get; set; } = new();

        public string Mensagem
        {
            get
            {
                var texto = Aprovados > 0
                    ? $"{Aprovados} sugestão(ões) aprovada(s), totalizando {Fmt.Money(ValorTotal)}."
                    : "Nenhuma sugestão foi aprovada.";
                if (Conflitos > 0) texto += $" {Conflitos} conflito(s).";
                if (Erros > 0) texto += $" {Erros} erro(s).";
                foreach (var atencao in Atencoes.Take(3)) texto += $" {atencao}";
                if (Atencoes.Count > 3) texto += " ...";
                return texto;
            }
        }
    }

    // Relatório de conciliação: SOMENTE contagens. Não cria sugestões nem lançamentos
    // e não mostra nomes, descrições nem valores individuais.
    public class RelatorioConciliacao
    {
        public int JanelaDias { get; set; }       // tamanho da janela da reconciliação leve
        public DateTime GeradoEm { get; set; }
        public int TotalAntigas { get; set; }     // OS fechadas antes da janela
        public int Ate30Dias { get; set; }
        public int De31A90Dias { get; set; }
        public int MaisDe90Dias { get; set; }
        public int SemValor { get; set; }
        public int Quitadas { get; set; }
        public int Parciais { get; set; }
        public int SemRecebimento { get; set; }
        public int RecebidoACMais { get; set; }
        public int CanceladasComRecebido { get; set; }
        public int SugestoesPendentes { get; set; }
        public int SugestoesAprovadas { get; set; }
        public int SugestoesIgnoradas { get; set; }
        public int SugestoesCanceladas { get; set; }
    }

    // ==========================================================================================
    // OsFinancialService — ÚNICA fonte de verdade do financeiro ligado a Ordens de Serviço.
    //
    // Antes tinha conta de ValorRecebido espalhada em três actions diferentes. Agora:
    //   - RecalcularPagamentoDaOS: recalcula a OS a partir das entradas que realmente existem;
    //   - ReconciliarOS: cria/atualiza/cancela a sugestão conforme as regras R1 a R6;
    //   - AprovarSugestao: SOMENTE aqui um lançamento é gravado, e exige clique de OK;
    //   - RejeitarSugestao: guarda o motivo e não reaparece sozinha.
    //
    // Todos os métodos são idempotentes: rodar duas vezes não duplica nada.
    // ==========================================================================================
    public class OsFinancialService
    {
        // Quantos dias de histórico a reconciliação "leve" das telas visa.
        // Serve para consertar algum evento perdido enquanto o Render estava hibernando,
        // sem criar sugestões para o histórico antigo em massa (isso é decisão explícita).
        private const int DiasReconciliacaoLeve = 30;

        private readonly AppDbContext _context;

        public OsFinancialService(AppDbContext context)
        {
            _context = context;
        }

        // --------------------------------------------------------------------------------------
        // RecalcularPagamentoDaOS
        // Soma as ENTRADAS ligadas à OS e grava: ValorRecebido, PaymentStatus, PaymentDate
        // e PaymentMethod (do recebimento mais recente). Chamar de novo dá o mesmo resultado.
        // "salvar: false" deixa o chamador juntar isso com outras alterações num único SaveChanges.
        // --------------------------------------------------------------------------------------
        public async Task RecalcularPagamentoDaOS(int osId, bool salvar = true, CancellationToken ct = default)
        {
            var pedido = await _context.ServiceOrders.FirstOrDefaultAsync(o => o.Id == osId, ct);
            if (pedido == null) return; // OS inexistente: nada a fazer

            // Entradas ligadas à OS (tanto as geradas pelo sistema quanto as manuais que
            // passarem a ser vinculadas na Fase 2). Saídas nunca entram nesta conta.
            var entradas = await _context.FinancialEntries.AsNoTracking()
                .Where(f => f.ServiceOrderId == osId && f.Tipo == EntryType.Entrada)
                .OrderByDescending(f => f.Data).ThenByDescending(f => f.Id)
                .ToListAsync(ct);

            var total = Arredondar(entradas.Sum(f => f.Valor)); // nunca negativo
            var ultima = entradas.FirstOrDefault();            // mais recente: data maior, depois maior Id

            pedido.ValorRecebido = total;
            pedido.PaymentDate = ultima?.Data;
            pedido.PaymentMethod = ultima?.FormaPagamento;

            // "A combinar" (sem preço): o próprio recebido é a referência, igual ao fluxo antigo.
            var devido = pedido.FinalPrice ?? pedido.EstimatedPrice ?? total;
            pedido.PaymentStatus = total > 0m && total >= devido
                ? OrderPaymentStatus.Paid
                : OrderPaymentStatus.Pending;

            if (salvar) await _context.SaveChangesAsync(ct);
        }

        // --------------------------------------------------------------------------------------
        // ReconciliarOS — regras R1 a R6
        // --------------------------------------------------------------------------------------
        public async Task ReconciliarOS(int osId, CancellationToken ct = default)
        {
            var pedido = await _context.ServiceOrders
                .Include(o => o.Service)
                .FirstOrDefaultAsync(o => o.Id == osId, ct);
            if (pedido == null) return;

            // Antes de decidir, garante que os números da OS batem com as entradas.
            await RecalcularPagamentoDaOS(osId, salvar: false, ct);

            var pendente = await _context.FinancialSuggestions
                .FirstOrDefaultAsync(s => s.ServiceOrderId == osId && s.Status == SuggestionStatus.Pendente, ct);

            var combinado = pedido.FinalPrice ?? pedido.EstimatedPrice;
            var saldo = combinado.HasValue ? Arredondar(Math.Max(0m, combinado.Value - pedido.ValorRecebido)) : 0m;

            // R4: OS cancelada -> cancela a pendente e avisa se já havia dinheiro recebido.
            // Nunca criamos uma saída automática: devolução/estorno é revisão humana.
            if (pedido.Status == OrderStatus.Cancelled)
            {
                if (pendente != null) Cancelar(pendente, "OS cancelada");
                if (pedido.ValorRecebido > 0m)
                {
                    await AvisarUmaVez(NotificationType.Info,
                        $"Revisar recebimento da OS-{pedido.Id:D5}",
                        "A OS foi cancelada com valor recebido. Confira se precisa devolver ou estornar manualmente.",
                        pedido.Id, ct);
                }
                await _context.SaveChangesAsync(ct);
                return;
            }

            // Se a OS saiu de "Concluída" (voltou para andamento, por exemplo),
            // uma sugestão pendente não faz mais sentido.
            if (pedido.Status != OrderStatus.Completed)
            {
                if (pendente != null) Cancelar(pendente, "OS não está concluída");
                await _context.SaveChangesAsync(ct);
                return;
            }

            // R5: concluída sem valor definido -> só aviso, nunca sugestão de R$ 0,00.
            if (!combinado.HasValue || combinado.Value <= 0m)
            {
                if (pendente != null) Cancelar(pendente, "OS sem valor definido");
                await AvisarUmaVez(NotificationType.Suggestion,
                    $"Defina o valor final da OS-{pedido.Id:D5}",
                    "Sem valor combinado não dá para sugerir um recebimento.",
                    pedido.Id, ct);
                await _context.SaveChangesAsync(ct);
                return;
            }

            // Recebido a mais: nunca gerar valor negativo. Aviso para revisão manual.
            if (pedido.ValorRecebido > combinado.Value)
            {
                if (pendente != null) Cancelar(pendente, "Recebido a mais");
                await AvisarUmaVez(NotificationType.Info,
                    $"Recebido a mais na OS-{pedido.Id:D5}",
                    "O total recebido passou do valor combinado. Confira se há valor a devolver.",
                    pedido.Id, ct);
                await _context.SaveChangesAsync(ct);
                return;
            }

            // Já está quitado: se sobrou uma pendente antiga, encerra ela.
            if (saldo <= 0m)
            {
                if (pendente != null) Cancelar(pendente, "Saldo quitado");
                await _context.SaveChangesAsync(ct);
                return;
            }

            if (pendente != null)
            {
                // R2: o preço (ou o já recebido) mudou enquanto a sugestão esperava.
                // Atualiza o valor em vez de criar outra. A data original é mantida.
                if (pendente.Valor != saldo)
                {
                    pendente.Valor = saldo;
                    pendente.MotivoCriacao = "Saldo recalculado da OS";
                }
                pendente.Categoria = await CategoriaDerivadaAsync(pedido, ct);
                pendente.Token = Guid.NewGuid(); // invalida formulários abertos antes da mudança
            }
            else
            {
                // R6: uma sugestão IGNORADA só volta se o preço ou o recebido mudou.
                var ultimaIgnorada = await _context.FinancialSuggestions
                    .Where(s => s.ServiceOrderId == osId && s.Status == SuggestionStatus.Rejeitada)
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync(ct);

                if (ultimaIgnorada != null &&
                    ultimaIgnorada.ValorCombinadoNaCriacao == combinado.Value &&
                    ultimaIgnorada.ValorRecebidoNaCriacao == pedido.ValorRecebido)
                {
                    await _context.SaveChangesAsync(ct); // guarda só o recálculo da OS
                    return;
                }

                var sugestao = new FinancialSuggestion
                {
                    ServiceOrderId = pedido.Id,
                    Valor = saldo,
                    DataSugerida = Clock.Today,
                    FormaPagamentoSugerida = await FormaSugeridaAsync(pedido, ct),
                    SocioSugerido = "Empresa",
                    Categoria = await CategoriaDerivadaAsync(pedido, ct),
                    Descricao = $"Receber saldo da OS-{pedido.Id:D5}",
                    MotivoCriacao = "OS concluída",
                    Status = SuggestionStatus.Pendente,
                    CriadaEm = Clock.Now,
                    Token = Guid.NewGuid(),
                    ValorCombinadoNaCriacao = Arredondar(combinado.Value),
                    ValorRecebidoNaCriacao = pedido.ValorRecebido
                };
                _context.FinancialSuggestions.Add(sugestao);

                await AvisarUmaVez(NotificationType.Suggestion,
                    $"Recebimento sugerido da OS-{pedido.Id:D5}",
                    $"{Fmt.Money(saldo)} aguardando seu OK.",
                    pedido.Id, ct);
            }

            await _context.SaveChangesAsync(ct);
        }

        // --------------------------------------------------------------------------------------
        // AprovarSugestao — o ÚNICO lugar que grava uma entrada financeira de OS.
        // Exige token (do formulário) + status Pendente. Falha de concorrência não grava nada.
        // --------------------------------------------------------------------------------------
        public async Task<ResultadoSugestao> AprovarSugestao(int id, Guid token, string usuario,
            AjustesAprovacao? ajustes = null, CancellationToken ct = default)
        {
            // Checa o estado no BANCO antes de confiar na entidade em memória. Sem isso,
            // uma aba aberta antes da decisão guarda o token antigo no contexto e poderia
            // aprovar de novo (o PostgreSQL barra na gravação, mas o erro chega tarde).
            var estadoNoBanco = await _context.FinancialSuggestions.AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => new { s.Status, s.Token })
                .FirstOrDefaultAsync(ct);

            if (estadoNoBanco == null)
                return ResultadoSugestao.Erro("A sugestão não existe mais.");

            if (estadoNoBanco.Status != SuggestionStatus.Pendente || estadoNoBanco.Token != token)
                return new ResultadoSugestao { Conflito = true, Mensagem = "Esta sugestão já foi decidida em outra aba. A tela vai atualizar." };

            var sugestao = await _context.FinancialSuggestions
                .Include(s => s.ServiceOrder)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (sugestao == null)
                return ResultadoSugestao.Erro("A sugestão não existe mais.");

            var pedido = sugestao.ServiceOrder;
            if (pedido == null)
                return ResultadoSugestao.Erro("A OS desta sugestão não existe mais.");

            if (pedido.Status == OrderStatus.Cancelled)
                return ResultadoSugestao.Erro("Não é possível aprovar um recebimento de OS cancelada.");

            var valor = Arredondar(ajustes?.Valor ?? sugestao.Valor);
            if (valor <= 0m)
                return ResultadoSugestao.Erro("Informe um valor maior que zero.");

            var data = ajustes?.Data ?? sugestao.DataSugerida;
            var forma = ajustes?.FormaPagamento ?? sugestao.FormaPagamentoSugerida;
            var socio = string.IsNullOrWhiteSpace(ajustes?.Socio) ? sugestao.SocioSugerido : ajustes.Socio.Trim();
            var observacao = string.IsNullOrWhiteSpace(ajustes?.Observacao) ? null : ajustes.Observacao.Trim();

            var transacao = await IniciarTransacaoAsync(ct);
            try
            {
                // A entrada nasce AQUI, no clique do OK. Antes disso ela não existe.
                var entrada = new FinancialEntry
                {
                    Data = data,
                    Tipo = EntryType.Entrada,
                    Categoria = sugestao.Categoria,
                    Descricao = sugestao.Descricao,
                    Valor = valor,
                    PagadorRecebedor = pedido.ClientName,
                    FormaPagamento = forma,
                    ServicoPeca = pedido.ProductName,
                    Observacoes = observacao,
                    Origem = EntryOrigin.OS,
                    Socio = socio,
                    NumeroOs = pedido.Codigo,
                    ServiceOrderId = pedido.Id,
                    CreatedAt = Clock.Now
                };

                // Ligar pelo navigation resolve o FinancialEntryId sozinho, no mesmo SaveChanges.
                sugestao.FinancialEntry = entrada;
                sugestao.Status = SuggestionStatus.Aprovada;
                sugestao.DecididaEm = Clock.Now;
                sugestao.DecididoPor = usuario;
                sugestao.Token = Guid.NewGuid(); // qualquer formulário antigo deixa de valer

                Notificar(NotificationType.Payment, "Recebimento registrado",
                    $"OS-{pedido.Id:D5}: {Fmt.Money(valor)} via {forma.ToLabel()}", pedido.Id);

                // Primeiro grava a entrada (ela precisa existir no banco para ser somada),
                // depois recalcula a OS. As duas escritas ficam dentro da MESMA transação.
                await _context.SaveChangesAsync(ct);
                await RecalcularPagamentoDaOS(pedido.Id, ct: ct);

                if (transacao != null) await transacao.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (transacao != null) await transacao.RollbackAsync(ct);
                return new ResultadoSugestao { Conflito = true, Mensagem = "Esta sugestão já foi decidida em outra aba. A tela vai atualizar." };
            }
            catch
            {
                if (transacao != null) await transacao.RollbackAsync(ct);
                throw;
            }

            // Aprovação parcial: se sobrou saldo, a próxima sugestão nasce sozinha (R3).
            await ReconciliarOS(pedido.Id, ct);

            return new ResultadoSugestao
            {
                Sucesso = true,
                Valor = valor,
                CodigoOs = pedido.Codigo,
                OsId = pedido.Id,
                Mensagem = $"Recebimento de {Fmt.Money(valor)} da OS-{pedido.Id:D5} registrado."
            };
        }

        // --------------------------------------------------------------------------------------
        // AprovarLote — aprovação em massa com UM clique explícito no modal de confirmação.
        // Cada item passa pelo AprovarSugestao: mesmo token fresco, mesma transação,
        // mesmo recálculo da OS. Um conflito ou erro em um item não bloqueia os demais;
        // o resumo conta sucessos, conflitos e erros e soma só o que foi gravado.
        // --------------------------------------------------------------------------------------
        public async Task<ResultadoLote> AprovarLote(IReadOnlyList<ItemAprovacaoLote> itens,
            AjustesLote? ajustes, string usuario, CancellationToken ct = default)
        {
            var resultado = new ResultadoLote();

            if (itens == null || itens.Count == 0)
            {
                resultado.Erros = 1;
                resultado.Atencoes.Add("Selecione ao menos uma sugestão.");
                return resultado;
            }

            // Uma mesma sugestão não entra duas vezes no lote, mesmo que o formulário
            // tenha sido manipulado. O teto é o mesmo das telas (LimiteLinhas).
            var unicos = itens.Where(i => i.Id > 0).GroupBy(i => i.Id).Select(g => g.First()).ToList();
            if (unicos.Count == 0)
            {
                resultado.Erros = 1;
                resultado.Atencoes.Add("Selecione ao menos uma sugestão válida.");
                return resultado;
            }
            if (unicos.Count > 300)
            {
                resultado.Erros = 1;
                resultado.Atencoes.Add("O lote passou do limite de 300 sugestões por vez.");
                return resultado;
            }

            // O estado atual e o token vêm do banco, nunca só do formulário. Assim uma
            // aba aberta antes de uma decisão não aprova uma sugestão que já mudou.
            var ids = unicos.Select(i => i.Id).ToList();
            var noBanco = await _context.FinancialSuggestions.AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .Select(s => new { s.Id, s.Token, s.Status, s.ServiceOrderId })
                .ToListAsync(ct);
            var porId = noBanco.ToDictionary(x => x.Id);

            var ajustesIndividuais = new AjustesAprovacao
            {
                Data = ajustes?.Data,
                FormaPagamento = ajustes?.FormaPagamento,
                Observacao = ajustes?.Observacao
                // Valor e Socio ficam nulos: cada sugestão mantém os seus.
            };

            foreach (var item in unicos)
            {
                if (!porId.TryGetValue(item.Id, out var atual))
                {
                    resultado.Conflitos++;
                    resultado.Atencoes.Add("Uma sugestão selecionada não existe mais.");
                    continue;
                }

                var codigo = $"OS-{atual.ServiceOrderId:D5}";
                if (atual.Status != SuggestionStatus.Pendente || atual.Token != item.Token)
                {
                    resultado.Conflitos++;
                    resultado.Atencoes.Add($"Conflito na {codigo}: já decidida ou token alterado.");
                    continue;
                }

                try
                {
                    // A gravação acontece aqui, dentro da aprovação individual, nunca antes.
                    var decisao = await AprovarSugestao(item.Id, item.Token, usuario, ajustesIndividuais, ct);
                    if (decisao.Sucesso)
                    {
                        resultado.Aprovados++;
                        resultado.ValorTotal = Arredondar(resultado.ValorTotal + decisao.Valor);
                    }
                    else if (decisao.Conflito)
                    {
                        resultado.Conflitos++;
                        resultado.Atencoes.Add($"Conflito na {codigo}: outra aba decidiu primeiro.");
                    }
                    else
                    {
                        resultado.Erros++;
                        resultado.Atencoes.Add($"Não foi possível aprovar a {codigo}: {decisao.Mensagem}");
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Não devolvemos ex.Message: um erro de banco pode revelar detalhes técnicos.
                    resultado.Erros++;
                    resultado.Atencoes.Add($"Falha ao gravar a {codigo}.");
                }
            }

            return resultado;
        }

        // --------------------------------------------------------------------------------------
        // RejeitarSugestao — "Ignorar" com motivo curto. Não apaga nada e não cria entrada.
        // --------------------------------------------------------------------------------------
        public async Task<ResultadoSugestao> RejeitarSugestao(int id, Guid token, string motivo,
            string usuario, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(motivo))
                return ResultadoSugestao.Erro("Informe o motivo para ignorar.");

            // Mesma checagem fresca da aprovação: token antigo nunca decide duas vezes.
            var estadoNoBanco = await _context.FinancialSuggestions.AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => new { s.Status, s.Token })
                .FirstOrDefaultAsync(ct);

            if (estadoNoBanco == null)
                return ResultadoSugestao.Erro("A sugestão não existe mais.");

            if (estadoNoBanco.Status != SuggestionStatus.Pendente || estadoNoBanco.Token != token)
                return new ResultadoSugestao { Conflito = true, Mensagem = "Esta sugestão já foi decidida em outra aba. A tela vai atualizar." };

            var sugestao = await _context.FinancialSuggestions
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (sugestao == null)
                return ResultadoSugestao.Erro("A sugestão não existe mais.");

            sugestao.Status = SuggestionStatus.Rejeitada;
            sugestao.DecididaEm = Clock.Now;
            sugestao.DecididoPor = usuario;
            sugestao.MotivoRejeicao = motivo.Trim();
            sugestao.Token = Guid.NewGuid();

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new ResultadoSugestao { Conflito = true, Mensagem = "Esta sugestão já foi decidida em outra aba. A tela vai atualizar." };
            }

            return new ResultadoSugestao { Sucesso = true, OsId = sugestao.ServiceOrderId, Mensagem = "Sugestão ignorada." };
        }

        // --------------------------------------------------------------------------------------
        // Reconciliação leve chamada ao abrir Visão geral e Financeiro.
        // Só olha OS concluídas/canceladas mexidas nos últimos DiasReconciliacaoLeve.
        // --------------------------------------------------------------------------------------
        public async Task ReconciliarRecentesAsync(CancellationToken ct = default)
        {
            var limite = Clock.Today.AddDays(-DiasReconciliacaoLeve).ToDateTime(TimeOnly.MinValue);
            var ids = await _context.ServiceOrders
                .Where(o => (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Cancelled)
                            && o.UpdatedAt >= limite)
                .Select(o => o.Id)
                .ToListAsync(ct);

            foreach (var id in ids) await ReconciliarOS(id, ct);
        }

        // --------------------------------------------------------------------------------------
        // Relatório de conciliação do histórico antigo (fora da janela leve).
        // SOMENTE LEITURA: não chama ReconciliarOS, não cria sugestões e não grava entries.
        // As consultas trazem só números e enums: nenhum nome/ementa sai do banco.
        // --------------------------------------------------------------------------------------
        public async Task<RelatorioConciliacao> GerarRelatorioConciliacaoAsync(CancellationToken ct = default)
        {
            var relatorio = new RelatorioConciliacao
            {
                JanelaDias = DiasReconciliacaoLeve,
                GeradoEm = Clock.Now
            };

            var limite = Clock.Today.AddDays(-DiasReconciliacaoLeve).ToDateTime(TimeOnly.MinValue);
            var limite90 = Clock.Today.AddDays(-90).ToDateTime(TimeOnly.MinValue);

            var fechadas = _context.ServiceOrders.AsNoTracking()
                .Where(o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.Cancelled);

            relatorio.Ate30Dias = await fechadas.CountAsync(o => o.UpdatedAt >= limite, ct);
            relatorio.De31A90Dias = await fechadas.CountAsync(o => o.UpdatedAt < limite && o.UpdatedAt >= limite90, ct);
            relatorio.MaisDe90Dias = await fechadas.CountAsync(o => o.UpdatedAt < limite90, ct);

            // Antigas = exatamente o conjunto que a reconciliação leve não visita.
            var antigas = await fechadas.Where(o => o.UpdatedAt < limite)
                .Select(o => new
                {
                    o.Status,
                    Devido = (decimal?)(o.FinalPrice ?? o.EstimatedPrice) ?? 0m,
                    o.ValorRecebido
                })
                .ToListAsync(ct);

            relatorio.TotalAntigas = antigas.Count;
            relatorio.SemValor = antigas.Count(x => x.Status == OrderStatus.Completed && x.Devido <= 0m);
            relatorio.Quitadas = antigas.Count(x => x.Status == OrderStatus.Completed && x.Devido > 0m && x.ValorRecebido >= x.Devido);
            relatorio.Parciais = antigas.Count(x => x.Status == OrderStatus.Completed && x.ValorRecebido > 0m && x.ValorRecebido < x.Devido);
            relatorio.SemRecebimento = antigas.Count(x => x.Status == OrderStatus.Completed && x.Devido > 0m && x.ValorRecebido <= 0m);
            relatorio.RecebidoACMais = antigas.Count(x => x.Status == OrderStatus.Completed && x.Devido > 0m && x.ValorRecebido > x.Devido);
            relatorio.CanceladasComRecebido = antigas.Count(x => x.Status == OrderStatus.Cancelled && x.ValorRecebido > 0m);

            var porStatus = await _context.FinancialSuggestions.AsNoTracking()
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Total = g.Count() })
                .ToListAsync(ct);

            relatorio.SugestoesPendentes = porStatus.FirstOrDefault(x => x.Status == SuggestionStatus.Pendente)?.Total ?? 0;
            relatorio.SugestoesAprovadas = porStatus.FirstOrDefault(x => x.Status == SuggestionStatus.Aprovada)?.Total ?? 0;
            relatorio.SugestoesIgnoradas = porStatus.FirstOrDefault(x => x.Status == SuggestionStatus.Rejeitada)?.Total ?? 0;
            relatorio.SugestoesCanceladas = porStatus.FirstOrDefault(x => x.Status == SuggestionStatus.Cancelada)?.Total ?? 0;

            return relatorio;
        }

        // --------------------------------------------------------------------------------------
        // Métodos privados de apoio
        // --------------------------------------------------------------------------------------

        // Forma de pagamento sugerida: a mais usada nos recebimentos anteriores do mesmo
        // cliente; se nunca houve recebimento, Pix. (O nome fica só na consulta, nunca em log.)
        private async Task<PaymentMethod> FormaSugeridaAsync(ServiceOrder pedido, CancellationToken ct)
        {
            var maisUsada = await _context.FinancialEntries.AsNoTracking()
                .Where(f => f.Tipo == EntryType.Entrada
                            && f.PagadorRecebedor == pedido.ClientName
                            && f.ServiceOrderId != pedido.Id)
                .GroupBy(f => f.FormaPagamento)
                .Select(g => new { Forma = g.Key, Quantidade = g.Count(), UltimoId = g.Max(f => f.Id) })
                .OrderByDescending(x => x.Quantidade).ThenByDescending(x => x.UltimoId)
                .FirstOrDefaultAsync(ct);

            return maisUsada?.Forma ?? PaymentMethod.Pix;
        }

        // Categoria do lançamento derivada do serviço do catálogo (ou do equipamento,
        // quando a OS não tem serviço). Usa apenas as categorias já existentes no sistema.
        private async Task<string> CategoriaDerivadaAsync(ServiceOrder pedido, CancellationToken ct)
        {
            var servico = pedido.Service ?? await _context.Services.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == pedido.ServiceId, ct);

            var titulo = (servico?.Title ?? pedido.ProductName ?? string.Empty).ToLowerInvariant();
            var classe = (servico?.Class ?? string.Empty).ToLowerInvariant();

            if (titulo.Contains("recuper") || classe.Contains("dados")) return "Recuperacao de Dados";
            if (titulo.Contains("format") || titulo.Contains("reinstal")) return "Formatacao e Reinstalacao";
            if (titulo.Contains("montagem") || titulo.Contains("upgrade") || classe.Contains("hardware")) return "Upgrade de Hardware";
            if (titulo.Contains("remoto") || classe.Contains("consultoria")) return "Suporte Remoto";
            return "Servico de Manutencao";
        }

        // Encerra uma sugestão pendente sem apagar histórico.
        private static void Cancelar(FinancialSuggestion sugestao, string motivo)
        {
            sugestao.Status = SuggestionStatus.Cancelada;
            sugestao.DecididaEm = Clock.Now;
            sugestao.DecididoPor = "Sistema";
            sugestao.MotivoRejeicao = motivo;
            sugestao.Token = Guid.NewGuid();
        }

        // Cria um aviso no sino no máximo UMA vez por OS/título. Sem isso, a reconciliação
        // leve das telas encheria o sino de notificações repetidas toda vez que a página abre.
        private async Task AvisarUmaVez(NotificationType tipo, string titulo, string mensagem,
            int osId, CancellationToken ct)
        {
            var jaExiste = await _context.Notifications
                .AnyAsync(n => n.RefType == "order" && n.RefId == osId && n.Title == titulo, ct);
            if (jaExiste) return;

            Notificar(tipo, titulo, mensagem, osId);
        }

        // Cria a notificação em memória; quem chama decide quando salvar.
        private void Notificar(NotificationType tipo, string titulo, string mensagem, int osId)
        {
            _context.Notifications.Add(new Notification
            {
                Type = tipo,
                Title = titulo,
                Message = mensagem,
                RefId = osId,
                RefType = "order",
                IsRead = false,
                CreatedAt = Clock.Now
            });
        }

        // PostgreSQL abre transação de verdade. O banco em memória dos testes não aceita
        // transação; nele um único SaveChanges já é atômico.
        private async Task<IDbContextTransaction?> IniciarTransacaoAsync(CancellationToken ct)
        {
            if (_context.Database.CurrentTransaction != null) return null; // já estamos dentro de uma
            if (_context.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
                return null;

            return await _context.Database.BeginTransactionAsync(ct);
        }

        // Dinheiro sempre com duas casas, arredondando para cima na metade do centavo
        // (ex.: 10,005 vira 10,01). Evita sobras de 0,001 vindas de formulário.
        private static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }
}
