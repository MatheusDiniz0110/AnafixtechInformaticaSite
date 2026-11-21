using AnafixtechInformaticaSite.Data;
using AnafixtechInformaticaSite.Models;
using AnafixtechInformaticaSite.Models.ViewModels;
using AnafixtechInformaticaSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnafixtechInformaticaSite.Tests;

// Fase 3: custo da peça vinculada, lucro realizado e lembrete de 3 dias.
// Todos os dados abaixo são FICTÍCIOS e não identificam ninguém.
public class Fase3CustoLucroLembreteTests
{
    private static StockItem NovaPeca(AppDbContext db, decimal? preco)
    {
        var peca = new StockItem
        {
            Name = "Peca Ficticia de teste",
            Category = "outro",
            Quantity = 1,
            MinQuantity = 0,
            PurchasePrice = preco
        };
        db.StockItems.Add(peca);
        db.SaveChanges();
        return peca;
    }

    [Fact]
    public async Task CalcularLucroDaOS_ComPecaVinculada_UsaRecebidoMenosPrecoDeCompra()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var peca = NovaPeca(db, 120m);
        pedido.StockItemId = peca.Id;
        db.NovaEntrada(pedido.Id, 300m);
        await db.SaveChangesAsync();
        var servico = new OsFinancialService(db);

        var resultado = await servico.CalcularLucroDaOS(pedido.Id);

        Assert.Equal(pedido.Id, resultado.OsId);
        Assert.Equal(300m, resultado.Recebido);
        Assert.Equal(120m, resultado.CustoPecas);
        Assert.True(resultado.CustoInformado);
        Assert.Equal(180m, resultado.Lucro);
        Assert.Single(db.FinancialEntries); // cálculo não gravou nada
    }

    [Fact]
    public async Task CalcularLucroDaOS_SemPrecoOuSemPeca_CustoNaoInformadoELucroNulo()
    {
        using var db = TestHelpers.NovoBanco();
        var pedidoComPeca = db.NovaOs(valorFinal: 200m);
        var pedidoSemPeca = db.NovaOs(valorFinal: 200m);
        pedidoComPeca.StockItemId = NovaPeca(db, null).Id;
        db.NovaEntrada(pedidoComPeca.Id, 200m);
        await db.SaveChangesAsync();
        var servico = new OsFinancialService(db);

        var comPeca = await servico.CalcularLucroDaOS(pedidoComPeca.Id);
        var semPeca = await servico.CalcularLucroDaOS(pedidoSemPeca.Id);

        Assert.Equal(200m, comPeca.Recebido);
        Assert.Null(comPeca.CustoPecas);
        Assert.Null(comPeca.Lucro);
        Assert.False(comPeca.CustoInformado);
        Assert.Null(semPeca.CustoPecas); // custo ausente nunca é zero
        Assert.Null(semPeca.Lucro);
    }

    [Fact]
    public async Task CalcularLucroDaOS_PendenteNaoContaComoRecebidoAteAprovacao()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 400m);
        pedido.StockItemId = NovaPeca(db, 100m).Id;
        await db.SaveChangesAsync();
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var sugestao = await db.FinancialSuggestions.SingleAsync();

        var antes = await servico.CalcularLucroDaOS(pedido.Id);
        Assert.Equal(0m, antes.Recebido);
        Assert.Equal(100m, antes.CustoPecas);
        Assert.Empty(db.FinancialEntries);

        var aprovacao = await servico.AprovarSugestao(sugestao.Id, sugestao.Token, "Admin Ficticio");
        Assert.True(aprovacao.Sucesso);

        var depois = await servico.CalcularLucroDaOS(pedido.Id);
        Assert.Equal(400m, depois.Recebido);
        Assert.Equal(300m, depois.Lucro);
    }

    [Fact]
    public async Task AvisarSugestoesPendentesAntigasAsync_ComTresDias_CriaUmAvisoSoESemDadosPessoais()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        pedido.ClientName = "Nome Sensivel Ficticio";
        await db.SaveChangesAsync();
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var sugestao = await db.FinancialSuggestions.SingleAsync();
        sugestao.DataSugerida = Clock.Today.AddDays(-3);
        await db.SaveChangesAsync();

        await servico.AvisarSugestoesPendentesAntigasAsync();
        await servico.AvisarSugestoesPendentesAntigasAsync();

        // ReconciliarOS já gera o aviso inicial; aqui contamos só o lembrete novo.
        var aviso = Assert.Single(db.Notifications.Where(n => n.Title.StartsWith("Lembrete:", StringComparison.Ordinal)));
        Assert.Equal(NotificationType.Suggestion, aviso.Type);
        Assert.Equal("order", aviso.RefType);
        Assert.Equal(pedido.Id, aviso.RefId);
        Assert.Contains("Lembrete", aviso.Title, StringComparison.Ordinal);
        Assert.DoesNotContain("Nome Sensivel Ficticio", aviso.Title + aviso.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SuggestionStatus.Pendente, (await db.FinancialSuggestions.SingleAsync()).Status);
        Assert.Empty(db.FinancialEntries);
    }

    [Fact]
    public async Task AvisarSugestoesPendentesAntigasAsync_ComPendenteRecente_NaoAvisa()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var sugestao = await db.FinancialSuggestions.SingleAsync();
        sugestao.DataSugerida = Clock.Today.AddDays(-2);
        await db.SaveChangesAsync();

        await servico.AvisarSugestoesPendentesAntigasAsync();

        Assert.Empty(db.Notifications.Where(n => n.Title.StartsWith("Lembrete:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task PedidoDetalhes_ExpoeCustoELucroCalculados()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 250m);
        pedido.StockItemId = NovaPeca(db, 90m).Id;
        db.NovaEntrada(pedido.Id, 250m);
        await db.SaveChangesAsync();
        var controller = TestHelpers.NovoController(db);

        var resultado = await controller.PedidoDetalhes(pedido.Id);

        var view = Assert.IsType<ViewResult>(resultado);
        var vm = Assert.IsType<PedidoDetalhesViewModel>(view.Model);
        Assert.Equal(250m, vm.RecebidoRealizado);
        Assert.Equal(90m, vm.CustoPecas);
        Assert.Equal(160m, vm.LucroRealizado);
    }

    [Fact]
    public async Task Financeiro_ComPendenteAntiga_CriaLembreteEAtualizaContadorDoSino()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var sugestao = await db.FinancialSuggestions.SingleAsync();
        sugestao.DataSugerida = Clock.Today.AddDays(-3);
        await db.SaveChangesAsync();
        var controller = TestHelpers.NovoController(db);

        var resultado = await controller.Financeiro(null, null, null, null);

        Assert.IsType<ViewResult>(resultado);
        // Uma notificação inicial (da sugestão) + um lembrete; contador do sino reflete ambos.
        Assert.Single(db.Notifications.Where(n => n.Title.StartsWith("Lembrete:", StringComparison.Ordinal)));
        Assert.Equal(2, controller.ViewBag.NaoLidas);
        Assert.Empty(db.FinancialEntries);
    }
}
