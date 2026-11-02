using AnafixtechInformaticaSite.Models; // as classes (entidades) que viram tabelas
using Microsoft.EntityFrameworkCore;    // DbContext, DbSet e ModelConfigurationBuilder

namespace AnafixtechInformaticaSite.Data
{
    // O "AppDbContext" é a ponte entre o C# e o SQL Server: cada DbSet abaixo vira uma tabela.
    public class AppDbContext : DbContext
    {
        // Recebe as opções (string de conexão etc.) configuradas no Program.cs e repassa ao DbContext.
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // ---------- Tabelas que já existiam no seu projeto ----------
        // Uma única linha que representa a tabela inteira do banco de dados
        public DbSet<HomePageInformation> HomePageInformation { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Service> Services { get; set; }

        // ---------- Tabelas novas da dashboard (espelham as entidades do Base44) ----------
        public DbSet<ServiceOrder> ServiceOrders { get; set; }               // ordens de serviço
        public DbSet<StatusHistoryEntry> StatusHistory { get; set; }         // histórico de status das OS
        public DbSet<FinancialEntry> FinancialEntries { get; set; }          // lançamentos financeiros
        public DbSet<StockItem> StockItems { get; set; }                     // peças do estoque
        public DbSet<Agendamento> Agendamentos { get; set; }                 // compromissos da agenda
        public DbSet<HorarioDisponivel> HorariosDisponiveis { get; set; }    // horários disponíveis
        public DbSet<SupportTicket> SupportTickets { get; set; }             // tickets de suporte
        public DbSet<Notification> Notifications { get; set; }              // notificações do painel

        // Regras gerais aplicadas a TODAS as propriedades de um tipo, em todas as tabelas.
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Todo decimal (dinheiro) é gravado com 18 dígitos e 2 casas decimais.
            configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

            // Cada enum é gravado como TEXTO (ex.: "InProgress") em vez de número:
            // fica legível ao consultar o banco no SSMS e não quebra se a ordem do enum mudar.
            configurationBuilder.Properties<OrderStatus>().HaveConversion<string>();
            configurationBuilder.Properties<UrgencyLevel>().HaveConversion<string>();
            configurationBuilder.Properties<OrderPaymentStatus>().HaveConversion<string>();
            configurationBuilder.Properties<PaymentMethod>().HaveConversion<string>();
            configurationBuilder.Properties<EntryType>().HaveConversion<string>();
            configurationBuilder.Properties<EntryOrigin>().HaveConversion<string>();
            configurationBuilder.Properties<AgendamentoTipo>().HaveConversion<string>();
            configurationBuilder.Properties<AgendamentoStatus>().HaveConversion<string>();
            configurationBuilder.Properties<SlotStatus>().HaveConversion<string>();
            configurationBuilder.Properties<TicketPriority>().HaveConversion<string>();
            configurationBuilder.Properties<TicketStatus>().HaveConversion<string>();
            configurationBuilder.Properties<NotificationType>().HaveConversion<string>();
        }
    }
}