using System.Security.Claims;
using AnafixtechInformaticaSite.Controllers;
using AnafixtechInformaticaSite.Data;
using AnafixtechInformaticaSite.Models;
using AnafixtechInformaticaSite.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace AnafixtechInformaticaSite.Tests;

// Apenas infraestrutura de teste. Todos os nomes abaixo são FICTÍCIOS:
// nenhum dado de cliente, sócio ou dono do negócio aparece aqui.
internal static class TestHelpers
{
    // Banco em memória isolado por teste (cada Guid é um banco novo).
    internal static AppDbContext NovoBanco() => NovoBanco($"teste-{Guid.NewGuid()}");

    // Nome explícito: usado quando o teste precisa simular DUAS ABAS no mesmo banco.
    internal static AppDbContext NovoBanco(string nome)
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nome)
            .Options;
        return new AppDbContext(opcoes);
    }

    // Cria uma OS com valores neutros e fictícios.
    internal static ServiceOrder NovaOs(
        this AppDbContext db,
        OrderStatus status = OrderStatus.Completed,
        decimal? valorFinal = 300m,
        decimal? valorEstimado = null,
        DateTime? atualizadoEm = null)
    {
        var pedido = new ServiceOrder
        {
            ClientName = "Cliente Ficticio",
            ClientEmail = "cliente.ficticio@example.test",
            ClientPhone = "11999999999",
            ProductName = "Notebook de teste",
            ProblemDescription = "Problema ficticio usado apenas em testes automatizados.",
            Status = status,
            FinalPrice = valorFinal,
            EstimatedPrice = valorEstimado,
            CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0),
            UpdatedAt = atualizadoEm ?? Clock.Now
        };
        db.ServiceOrders.Add(pedido);
        db.SaveChanges();
        return pedido;
    }

    // Cria uma entrada financeira fictícia ligada a uma OS.
    internal static FinancialEntry NovaEntrada(
        this AppDbContext db,
        int osId,
        decimal valor,
        DateOnly? data = null,
        PaymentMethod forma = PaymentMethod.Pix)
    {
        var pedido = db.ServiceOrders.First(o => o.Id == osId);
        var entrada = new FinancialEntry
        {
            Data = data ?? Clock.Today,
            Tipo = EntryType.Entrada,
            Categoria = "Servico de Manutencao",
            Descricao = "Lancamento ficticio de teste",
            Valor = valor,
            PagadorRecebedor = pedido.ClientName,
            FormaPagamento = forma,
            Socio = "Empresa",
            NumeroOs = pedido.Codigo,
            ServiceOrderId = osId,
            Origem = EntryOrigin.OS,
            CreatedAt = Clock.Now
        };
        db.FinancialEntries.Add(entrada);
        db.SaveChanges();
        return entrada;
    }

    // Controller com HttpContext, TempData e usuário fictício, sem precisar subir o site.
    internal static DashboardController NovoController(AppDbContext db)
    {
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "Admin Ficticio") }, "teste"));

        var configs = new ConfigurationBuilder().Build(); // sem chave: não mexe com senhas
        var controller = new DashboardController(
            db,
            new AmbienteFicticio(),
            new AccountSecretProtector(configs),
            new OsFinancialService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        controller.TempData = new TempDataDictionary(http, new TempDataEmMemoria());
        return controller;
    }

    private sealed class AmbienteFicticio : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Testes";
        public string EnvironmentName { get; set; } = "Testes";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TempDataEmMemoria : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            // Os testes não leem mensagens da tela; só descartamos o que foi guardado.
        }
    }
}
