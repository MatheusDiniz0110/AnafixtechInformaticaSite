using AnafixtechInformaticaSite.Models;
using AnafixtechInformaticaSite.Models.ViewModels;
using AnafixtechInformaticaSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnafixtechInformaticaSite.Tests;

// Regressões das actions que mexem com dinheiro. Tudo com dados fictícios.
public class DashboardFinanceiroRegressionTests
{
    [Fact]
    public async Task PedidoPagamento_ComFormatoBrasileiro_GravaEntradaERecalculaOS()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 2000m);
        var controller = TestHelpers.NovoController(db);

        var resultado = await controller.PedidoPagamento(pedido.Id, "1.200,50", PaymentMethod.Pix, null, null);

        Assert.IsType<RedirectToActionResult>(resultado);
        var entrada = Assert.Single(db.FinancialEntries);
        Assert.Equal(1200.50m, entrada.Valor);
        Assert.Equal(EntryOrigin.OS, entrada.Origem);
        var atualizado = await db.ServiceOrders.SingleAsync(o => o.Id == pedido.Id);
        Assert.Equal(1200.50m, atualizado.ValorRecebido);
        Assert.Equal(OrderPaymentStatus.Pending, atualizado.PaymentStatus);
        Assert.Equal(799.50m, Assert.Single(db.FinancialSuggestions).Valor); // sugestão do saldo
    }

    [Fact]
    public async Task SugestaoAprovarEditada_ComVirgula_AceitaValorBrasileiro()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 2000m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var sugestao = await db.FinancialSuggestions.SingleAsync();
        var controller = TestHelpers.NovoController(db);

        var resultado = await controller.SugestaoAprovarEditada(sugestao.Id, sugestao.Token,
            "1.200,50", PaymentMethod.Pix, null, "Empresa", null);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(1200.50m, Assert.Single(db.FinancialEntries).Valor);
        Assert.Equal(799.50m, Assert.Single(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Pendente)).Valor);
    }

    [Fact]
    public async Task LancamentoEditar_RecalculaOSEAtualizaSugestao()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var entrada = db.NovaEntrada(pedido.Id, 100m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        Assert.Equal(200m, Assert.Single(db.FinancialSuggestions).Valor);
        var controller = TestHelpers.NovoController(db);

        var resultado = await controller.LancamentoEditar(new LancamentoFormViewModel
        {
            Id = entrada.Id,
            Data = Clock.Today,
            Tipo = EntryType.Entrada,
            Categoria = "Servico de Manutencao",
            Descricao = "Lancamento ficticio editado",
            Valor = "150,00",
            FormaPagamento = PaymentMethod.Pix,
            Socio = "Empresa"
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(150m, Assert.Single(db.FinancialEntries).Valor);
        Assert.Equal(150m, (await db.ServiceOrders.SingleAsync(o => o.Id == pedido.Id)).ValorRecebido);
        Assert.Equal(150m, Assert.Single(db.FinancialSuggestions).Valor);
    }

    [Fact]
    public async Task LancamentoExcluir_RecalculaOSEDevolveSaldoDaSugestao()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var entrada = db.NovaEntrada(pedido.Id, 100m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var controller = TestHelpers.NovoController(db);

        var resultado = await controller.LancamentoExcluir(entrada.Id);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Empty(db.FinancialEntries);
        Assert.Equal(0m, (await db.ServiceOrders.SingleAsync(o => o.Id == pedido.Id)).ValorRecebido);
        Assert.Equal(300m, Assert.Single(db.FinancialSuggestions).Valor);
    }

    [Fact]
    public async Task Financeiro_SugestoesPendentesNaoEntramNosTotais_AposOKEntram()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var controller = TestHelpers.NovoController(db);

        var antes = Assert.IsType<FinanceiroViewModel>(Assert.IsType<ViewResult>(await controller.Financeiro(null, null, null, null)).Model);
        Assert.Equal(0m, antes.Entradas);
        Assert.Single(antes.SugestoesPendentes);
        Assert.Equal(300m, antes.SugestoesPendentes.Sum(s => s.Valor));

        var sugestao = await db.FinancialSuggestions.SingleAsync();
        Assert.True((await servico.AprovarSugestao(sugestao.Id, sugestao.Token, "Admin Ficticio")).Sucesso);

        var depois = Assert.IsType<FinanceiroViewModel>(Assert.IsType<ViewResult>(await controller.Financeiro(null, null, null, null)).Model);
        Assert.Equal(300m, depois.Entradas);
        Assert.Empty(depois.SugestoesPendentes);
    }

    [Fact]
    public async Task Index_MostraSugestoesPendentesForaDosTotaisFinanceiros()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var controller = TestHelpers.NovoController(db);

        var vm = Assert.IsType<DashboardIndexViewModel>(Assert.IsType<ViewResult>(await controller.Index()).Model);

        Assert.Equal(1, vm.SugestoesPendentes);
        Assert.Equal(300m, vm.TotalSugestoesPendentes);
        Assert.Equal(0m, vm.EntradasMes);
        Assert.Equal(0m, vm.FaturamentoTotal);
        Assert.Empty(db.FinancialEntries);
    }
}
