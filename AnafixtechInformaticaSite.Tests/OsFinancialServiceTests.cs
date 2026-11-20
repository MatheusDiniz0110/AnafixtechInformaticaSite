using AnafixtechInformaticaSite.Models;
using AnafixtechInformaticaSite.Services;
using Microsoft.EntityFrameworkCore;

namespace AnafixtechInformaticaSite.Tests;

// Núcleo financeiro das OS (R1 a R6). Nenhum dado real: só números e nomes fictícios.
public class OsFinancialServiceTests
{
    // -----------------------------------------------------------------------------------------
    // R1 — total, parcial e excesso
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task ReconciliarOS_ConcluidaSemRecebimento_SugereValorTotalSemGravarEntrada()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);

        await servico.ReconciliarOS(pedido.Id);

        var sugestao = Assert.Single(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Pendente));
        Assert.Equal(300m, sugestao.Valor);
        Assert.Equal("OS concluída", sugestao.MotivoCriacao);
        Assert.Equal(pedido.Id, sugestao.ServiceOrderId);
        Assert.Empty(db.FinancialEntries); // REGRA DE OURO: nada foi gravado no financeiro
        Assert.Equal(0m, (await db.ServiceOrders.SingleAsync(o => o.Id == pedido.Id)).ValorRecebido);
    }

    [Fact]
    public async Task ReconciliarOS_ComRecebimentoParcial_SugereSomenteOSaldo()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        db.NovaEntrada(pedido.Id, 100m);
        var servico = new OsFinancialService(db);

        await servico.ReconciliarOS(pedido.Id);

        var sugestao = Assert.Single(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Pendente));
        Assert.Equal(200m, sugestao.Valor);
        var atualizado = await db.ServiceOrders.SingleAsync(o => o.Id == pedido.Id);
        Assert.Equal(100m, atualizado.ValorRecebido);
        Assert.Equal(OrderPaymentStatus.Pending, atualizado.PaymentStatus); // parcial continua Pendente
    }

    [Fact]
    public async Task ReconciliarOS_RecebidoACMais_NaoSugereValorNegativoEAlertar()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        db.NovaEntrada(pedido.Id, 400m);
        var servico = new OsFinancialService(db);

        await servico.ReconciliarOS(pedido.Id);

        Assert.Empty(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Pendente));
        Assert.Contains(await db.Notifications.ToListAsync(),
            n => n.Title.Contains("Recebido a mais", StringComparison.Ordinal));
        var atualizado = await db.ServiceOrders.SingleAsync(o => o.Id == pedido.Id);
        Assert.Equal(400m, atualizado.ValorRecebido);
        Assert.Equal(OrderPaymentStatus.Paid, atualizado.PaymentStatus);
    }

    // -----------------------------------------------------------------------------------------
    // R2 — preço alterado antes e depois de recebimentos
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task ReconciliarOS_PrecoAlteradoAntesDeRecebimentos_AtualizaMesmaSugestao()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var idOriginal = Assert.Single(db.FinancialSuggestions).Id;

        pedido.FinalPrice = 500m;
        await db.SaveChangesAsync();
        await servico.ReconciliarOS(pedido.Id);

        var sugestao = Assert.Single(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Pendente));
        Assert.Equal(idOriginal, sugestao.Id); // não cria outra
        Assert.Equal(500m, sugestao.Valor);
        Assert.Equal("Saldo recalculado da OS", sugestao.MotivoCriacao);
        Assert.Empty(db.FinancialEntries);
    }

    [Fact]
    public async Task ReconciliarOS_PrecoAlteradoDepoisDeRecebimento_RecalculaSaldo()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        db.NovaEntrada(pedido.Id, 100m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        Assert.Equal(200m, Assert.Single(db.FinancialSuggestions).Valor);

        pedido.FinalPrice = 400m;
        await db.SaveChangesAsync();
        await servico.ReconciliarOS(pedido.Id);

        Assert.Equal(300m, Assert.Single(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Pendente)).Valor);
    }

    // -----------------------------------------------------------------------------------------
    // R4 / R5 — cancelada com recebido e sem valor
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task ReconciliarOS_CanceladaComRecebido_CancelaSugestaoEAlertarSemEstornoAutomatico()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id); // cria a pendente
        db.NovaEntrada(pedido.Id, 100m);

        pedido.Status = OrderStatus.Cancelled;
        await db.SaveChangesAsync();
        await servico.ReconciliarOS(pedido.Id);

        var sugestao = Assert.Single(db.FinancialSuggestions);
        Assert.Equal(SuggestionStatus.Cancelada, sugestao.Status);
        Assert.Equal("OS cancelada", sugestao.MotivoRejeicao);
        Assert.Contains(await db.Notifications.ToListAsync(),
            n => n.Title.Contains("Revisar recebimento", StringComparison.Ordinal));
        Assert.Single(db.FinancialEntries); // devolução/estorno continua sendo decisão humana
    }

    [Fact]
    public async Task ReconciliarOS_ConcluidaSemValor_NaoCriaSugestaoDeZeroEAlertar()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: null, valorEstimado: null);
        var servico = new OsFinancialService(db);

        await servico.ReconciliarOS(pedido.Id);

        Assert.Empty(db.FinancialSuggestions);
        Assert.Contains(await db.Notifications.ToListAsync(),
            n => n.Type == NotificationType.Suggestion && n.Title.Contains("Defina o valor final", StringComparison.Ordinal));
    }

    // -----------------------------------------------------------------------------------------
    // Idempotência, aprovação, concorrência e arredondamento
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task ReconciliarOS_RodadaVariasVezes_NaoDuplicaSugestaoNemNotificacao()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);

        await servico.ReconciliarOS(pedido.Id);
        await servico.ReconciliarOS(pedido.Id);
        await servico.ReconciliarOS(pedido.Id);

        Assert.Single(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Pendente));
        Assert.Single(db.Notifications.Where(n => n.Title.Contains("Recebimento sugerido", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AprovarSugestao_DuploClique_GravaUmaUnicaEntrada()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var sugestao = await db.FinancialSuggestions.SingleAsync();

        var primeiro = await servico.AprovarSugestao(sugestao.Id, sugestao.Token, "Admin Ficticio");
        var segundo = await servico.AprovarSugestao(sugestao.Id, sugestao.Token, "Admin Ficticio");

        Assert.True(primeiro.Sucesso);
        Assert.True(segundo.Conflito);
        Assert.False(segundo.Sucesso);
        var entrada = Assert.Single(db.FinancialEntries);
        Assert.Equal(300m, entrada.Valor);
        Assert.Equal(EntryOrigin.OS, entrada.Origem);
        Assert.Equal(pedido.Id, entrada.ServiceOrderId);
        Assert.Equal(300m, (await db.ServiceOrders.SingleAsync(o => o.Id == pedido.Id)).ValorRecebido);
        Assert.Equal(OrderPaymentStatus.Paid, (await db.ServiceOrders.SingleAsync(o => o.Id == pedido.Id)).PaymentStatus);
        Assert.Equal(SuggestionStatus.Aprovada, (await db.FinancialSuggestions.SingleAsync()).Status);
    }

    [Fact]
    public async Task AprovarSugestao_DuasAbasComTokenAntigo_SegundaNaoGrava()
    {
        var nomeBanco = $"abaficticia-{Guid.NewGuid()}";
        using var aba1 = TestHelpers.NovoBanco(nomeBanco);
        using var aba2 = TestHelpers.NovoBanco(nomeBanco);

        var pedido = aba1.NovaOs(valorFinal: 300m);
        var servico1 = new OsFinancialService(aba1);
        var servico2 = new OsFinancialService(aba2);
        await servico1.ReconciliarOS(pedido.Id);

        // As duas abas carregam a MESMA sugestão (token idêntico).
        var sugestaoAba1 = await aba1.FinancialSuggestions.SingleAsync();
        var tokenAntigo = (await aba2.FinancialSuggestions.SingleAsync()).Token;
        Assert.Equal(sugestaoAba1.Token, tokenAntigo);

        var primeiro = await servico1.AprovarSugestao(sugestaoAba1.Id, sugestaoAba1.Token, "Admin Ficticio");
        var segundo = await servico2.AprovarSugestao(sugestaoAba1.Id, tokenAntigo, "Admin Ficticio");

        Assert.True(primeiro.Sucesso);
        Assert.True(segundo.Conflito);
        Assert.Single(aba1.FinancialEntries);
        Assert.Single(aba2.FinancialEntries);
    }

    [Fact]
    public async Task AprovarSugestao_ComAjusteQuebradoCentavo_ArredondaParaCima()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var sugestao = await db.FinancialSuggestions.SingleAsync();

        var resultado = await servico.AprovarSugestao(sugestao.Id, sugestao.Token, "Admin Ficticio",
            new AjustesAprovacao { Valor = 100.005m });

        Assert.True(resultado.Sucesso);
        Assert.Equal(100.01m, Assert.Single(db.FinancialEntries).Valor);
    }

    [Fact]
    public async Task AprovarSugestao_Parcial_GeraNovaSugestaoDoSaldo()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var sugestao = await db.FinancialSuggestions.SingleAsync();

        await servico.AprovarSugestao(sugestao.Id, sugestao.Token, "Admin Ficticio",
            new AjustesAprovacao { Valor = 100m });

        var aprovadas = Assert.Single(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Aprovada));
        var nova = Assert.Single(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Pendente));
        Assert.NotEqual(aprovadas.Id, nova.Id);
        Assert.Equal(200m, nova.Valor);
        Assert.Equal(100m, (await db.ServiceOrders.SingleAsync(o => o.Id == pedido.Id)).ValorRecebido);
    }

    [Fact]
    public async Task RejeitarSugestao_SoSemMudancaNaoVolta_MasVoltaSePrecoMuda()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var servico = new OsFinancialService(db);
        await servico.ReconciliarOS(pedido.Id);
        var sugestao = await db.FinancialSuggestions.SingleAsync();

        var ignorada = await servico.RejeitarSugestao(sugestao.Id, sugestao.Token,
            "Aguardando confirmacao", "Admin Ficticio");
        Assert.True(ignorada.Sucesso);
        Assert.Empty(db.FinancialEntries);

        await servico.ReconciliarOS(pedido.Id);
        Assert.Empty(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Pendente));

        pedido.FinalPrice = 350m;
        await db.SaveChangesAsync();
        await servico.ReconciliarOS(pedido.Id);

        // Sugestão ignorada não é dinheiro recebido: nada foi abatido, então o saldo é o valor novo.
        Assert.Equal(350m, Assert.Single(db.FinancialSuggestions.Where(s => s.Status == SuggestionStatus.Pendente)).Valor);
    }

    // -----------------------------------------------------------------------------------------
    // Janela leve e virada do dia
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task ReconciliarRecentesAsync_NaoGeraSugestoesParaHistoricoAntigo()
    {
        using var db = TestHelpers.NovoBanco();
        var antiga = db.NovaOs(valorFinal: 100m, atualizadoEm: Clock.Now.AddDays(-40));
        var recente = db.NovaOs(valorFinal: 200m, atualizadoEm: Clock.Now);
        var servico = new OsFinancialService(db);

        await servico.ReconciliarRecentesAsync();

        var sugestao = Assert.Single(db.FinancialSuggestions);
        Assert.Equal(recente.Id, sugestao.ServiceOrderId);
        Assert.DoesNotContain(antiga.Id, db.FinancialSuggestions.Select(s => s.ServiceOrderId));
    }

    [Fact]
    public async Task ViradaDoDia_SugestaoMantemDataDoDiaAnteriorAoAprovar()
    {
        using var db = TestHelpers.NovoBanco();
        var pedido = db.NovaOs(valorFinal: 300m);
        var ontem = Clock.Today.AddDays(-1);
        db.FinancialSuggestions.Add(new FinancialSuggestion
        {
            ServiceOrderId = pedido.Id,
            Valor = 300m,
            DataSugerida = ontem,
            FormaPagamentoSugerida = PaymentMethod.Pix,
            SocioSugerido = "Empresa",
            Categoria = "Servico de Manutencao",
            Descricao = "Receber saldo da OS de teste",
            MotivoCriacao = "OS concluída",
            Status = SuggestionStatus.Pendente,
            CriadaEm = Clock.Now.AddDays(-1),
            Token = Guid.NewGuid(),
            ValorCombinadoNaCriacao = 300m,
            ValorRecebidoNaCriacao = 0m
        });
        await db.SaveChangesAsync();
        var servico = new OsFinancialService(db);

        // "Amanhã": a reconciliação não muda a data sugerida original.
        await servico.ReconciliarOS(pedido.Id);
        var sugestao = await db.FinancialSuggestions.SingleAsync();
        Assert.Equal(ontem, sugestao.DataSugerida);

        var resultado = await servico.AprovarSugestao(sugestao.Id, sugestao.Token, "Admin Ficticio");
        Assert.True(resultado.Sucesso);
        Assert.Equal(ontem, Assert.Single(db.FinancialEntries).Data);
    }
}
