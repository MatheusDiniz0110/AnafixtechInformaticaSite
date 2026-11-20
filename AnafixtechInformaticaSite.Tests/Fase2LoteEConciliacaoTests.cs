using AnafixtechInformaticaSite.Models;
using AnafixtechInformaticaSite.Models.ViewModels;
using AnafixtechInformaticaSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnafixtechInformaticaSite.Tests;

// Fase 2: aprovação em lote e relatório de conciliação.
// Todos os nomes/descrições abaixo são FICTÍCIOS e não identificam ninguém.
public class Fase2LoteEConciliacaoTests
{
    [Fact]
    public async Task AprovarLote_ComAjusteComum_AtualizaDataFormaObservacaoEMantemValoresESocios()
    {
        using var db = TestHelpers.NovoBanco();
        var os1 = db.NovaOs(valorFinal: 300m);
        var os2 = db.NovaOs(valorFinal: 500m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(os1.Id);
        await servico.ReconciliarOS(os2.Id);

        var sugestoes = await db.FinancialSuggestions.OrderBy(s => s.Id).ToListAsync();
        sugestoes[1].SocioSugerido = "Socio Ficticio A"; // valor individual, não ajustado pelo lote
        await db.SaveChangesAsync();

        var dataComum = Clock.Today.AddDays(-2);
        var resultado = await servico.AprovarLote(
            sugestoes.Select(s => new ItemAprovacaoLote { Id = s.Id, Token = s.Token }).ToList(),
            new AjustesLote
            {
                Data = dataComum,
                FormaPagamento = PaymentMethod.Boleto,
                Observacao = "Ajuste comum ficticio do lote"
            },
            "Admin Ficticio");

        Assert.Equal(2, resultado.Aprovados);
        Assert.Equal(0, resultado.Conflitos);
        Assert.Equal(0, resultado.Erros);
        Assert.Equal(800m, resultado.ValorTotal);

        var entradas = await db.FinancialEntries.OrderBy(f => f.Id).ToListAsync();
        Assert.Equal(2, entradas.Count);
        Assert.All(entradas, e =>
        {
            Assert.Equal(dataComum, e.Data);
            Assert.Equal(PaymentMethod.Boleto, e.FormaPagamento);
            Assert.Equal("Ajuste comum ficticio do lote", e.Observacoes);
            Assert.Equal(EntryOrigin.OS, e.Origem);
        });
        Assert.Equal(300m, entradas[0].Valor);
        Assert.Equal(500m, entradas[1].Valor);
        Assert.Equal("Empresa", entradas[0].Socio);
        Assert.Equal("Socio Ficticio A", entradas[1].Socio); // sócio sugerido permanece individual
        Assert.Equal(OrderPaymentStatus.Paid, (await db.ServiceOrders.SingleAsync(o => o.Id == os1.Id)).PaymentStatus);
        Assert.Equal(OrderPaymentStatus.Paid, (await db.ServiceOrders.SingleAsync(o => o.Id == os2.Id)).PaymentStatus);
    }

    [Fact]
    public async Task AprovarLote_ComTokenAntigoEmUmItem_AprovaOsDemaisEContaConflito()
    {
        using var db = TestHelpers.NovoBanco();
        var os1 = db.NovaOs(valorFinal: 300m);
        var os2 = db.NovaOs(valorFinal: 500m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(os1.Id);
        await servico.ReconciliarOS(os2.Id);

        var sugestoes = await db.FinancialSuggestions.OrderBy(s => s.Id).ToListAsync();
        var itens = sugestoes.Select(s => new ItemAprovacaoLote { Id = s.Id, Token = s.Token }).ToList();

        // Simula outra aba tendo mexido na 2ª sugestão depois que o formulário abriu.
        sugestoes[1].Token = Guid.NewGuid();
        await db.SaveChangesAsync();

        var resultado = await servico.AprovarLote(itens, null, "Admin Ficticio");

        Assert.Equal(1, resultado.Aprovados);
        Assert.Equal(1, resultado.Conflitos);
        Assert.Equal(0, resultado.Erros);
        Assert.Equal(300m, resultado.ValorTotal);
        var entrada = Assert.Single(db.FinancialEntries);
        Assert.Equal(300m, entrada.Valor);
        Assert.Equal(os1.Id, entrada.ServiceOrderId);
        Assert.Equal(0m, (await db.ServiceOrders.SingleAsync(o => o.Id == os2.Id)).ValorRecebido);
        Assert.Contains("Conflito", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AprovarLote_SemSelecao_NaoGravaNada()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);

        var resultado = await servico.AprovarLote(new List<ItemAprovacaoLote>(), null, "Admin Ficticio");

        Assert.Equal(0, resultado.Aprovados);
        Assert.Equal(1, resultado.Erros);
        Assert.Empty(db.FinancialEntries);
        Assert.Equal(1, db.FinancialSuggestions.Count(s => s.Status == SuggestionStatus.Pendente));
    }

    [Fact]
    public async Task SugestoesAprovarTodas_IgnoraParesDoFormularioEUsaAsPendentesDoBanco()
    {
        using var db = TestHelpers.NovoBanco();
        var os1 = db.NovaOs(valorFinal: 300m);
        var os2 = db.NovaOs(valorFinal: 500m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(os1.Id);
        await servico.ReconciliarOS(os2.Id);
        var controller = TestHelpers.NovoController(db);

        var form = new AprovacaoLoteViewModel
        {
            AprovarTodas = true,
            Selecionadas = new List<string> { $"9999|{Guid.NewGuid()}" }, // dado inválido vindo do form
            Data = Clock.Today,
            FormaPagamento = PaymentMethod.Pix
        };

        var resultado = await controller.SugestoesAprovarLote(form);

        var redirecionamento = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Financeiro", redirecionamento.ActionName);
        Assert.Equal(2, db.FinancialEntries.Count());
        Assert.Empty(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Pendente));
        Assert.Contains("2 sugestão", controller.TempData["Ok"] as string, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SugestoesAprovarLote_ParIdTokenInvalido_NaoGrava()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var controller = TestHelpers.NovoController(db);

        var form = new AprovacaoLoteViewModel
        {
            Selecionadas = new List<string> { "isto-nao-e-um-par" }
        };

        var resultado = await controller.SugestoesAprovarLote(form);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Empty(db.FinancialEntries);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task GerarRelatorioConciliacao_SomenteLeitura_NaoCriaSugestoesNemContaNomes()
    {
        using var db = TestHelpers.NovoBanco();
        var antigaSemRecebimento = db.NovaOs(valorFinal: 300m, atualizadoEm: Clock.Now.AddDays(-40));
        var antigaQuitada = db.NovaOs(valorFinal: 200m, atualizadoEm: Clock.Now.AddDays(-120));
        db.NovaEntrada(antigaQuitada.Id, 200m);
        var canceladaComRecebido = db.NovaOs(
            status: OrderStatus.Cancelled, valorFinal: 100m, atualizadoEm: Clock.Now.AddDays(-45));
        db.NovaEntrada(canceladaComRecebido.Id, 50m);
        var recente = db.NovaOs(valorFinal: 500m, atualizadoEm: Clock.Now);

        var servico = new OsFinancialService(db);
        // Só recalcula os totais das OS (não cria sugestão): é o que o pagamento faria.
        await servico.RecalcularPagamentoDaOS(antigaQuitada.Id);
        await servico.RecalcularPagamentoDaOS(canceladaComRecebido.Id);

        var relatorio = await servico.GerarRelatorioConciliacaoAsync();

        Assert.Equal(3, relatorio.TotalAntigas);
        Assert.Equal(1, relatorio.Ate30Dias);
        Assert.Equal(2, relatorio.De31A90Dias);
        Assert.Equal(1, relatorio.MaisDe90Dias);
        Assert.Equal(0, relatorio.SemValor);
        Assert.Equal(1, relatorio.Quitadas);
        Assert.Equal(0, relatorio.Parciais);
        Assert.Equal(1, relatorio.SemRecebimento);
        Assert.Equal(0, relatorio.RecebidoACMais);
        Assert.Equal(1, relatorio.CanceladasComRecebido);
        Assert.Equal(0, relatorio.SugestoesPendentes);

        // Nada foi escrito: a OS antiga continua sem sugestão e sem aviso novo.
        Assert.Empty(db.FinancialSuggestions);
        Assert.Empty(db.Notifications);
        Assert.Equal(2, db.FinancialEntries.Count());
        Assert.DoesNotContain(antigaSemRecebimento.Id, db.FinancialSuggestions.Select(s => s.ServiceOrderId));
        Assert.DoesNotContain(recente.Id, db.FinancialSuggestions.Select(s => s.ServiceOrderId));
    }

    [Fact]
    public async Task Conciliacao_Action_AbreRelatorioSemDispararReconciliacao()
    {
        using var db = TestHelpers.NovoBanco();
        db.NovaOs(valorFinal: 300m, atualizadoEm: Clock.Now.AddDays(-40));
        db.NovaOs(valorFinal: 500m, atualizadoEm: Clock.Now);
        var controller = TestHelpers.NovoController(db);

        var resultado = await controller.Conciliacao();

        var view = Assert.IsType<ViewResult>(resultado);
        Assert.IsType<RelatorioConciliacao>(view.Model);
        Assert.Empty(db.FinancialSuggestions);
        Assert.Empty(db.FinancialEntries);
    }

    [Fact]
    public async Task ResultadoLote_MensagemDeResumo_NaoExpoeDadosPessoais()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        pedido.ClientName = "Nome Sensivel Ficticio"; // dado inventado, usado só para checar vazamento
        await db.SaveChangesAsync();
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var sugestao = await db.FinancialSuggestions.SingleAsync();

        var resultado = await servico.AprovarLote(
            new List<ItemAprovacaoLote> { new() { Id = sugestao.Id, Token = sugestao.Token } },
            null, "Admin Ficticio");

        Assert.DoesNotContain("Nome Sensivel Ficticio", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("300", resultado.Mensagem, StringComparison.Ordinal);
    }
}
