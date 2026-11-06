using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Reflection;

namespace AnafixtechInformaticaSite.Data
{
    // Usado apenas pelo "dotnet ef" (migrations). Em tempo de execução a configuração vem do
    // Program.cs. Aqui lemos appsettings, user secrets e variáveis de ambiente, na mesma ordem
    // de precedência da aplicação, para que "dotnet ef database update" funcione localmente e
    // também contra o banco do Render quando a connection string vier do ambiente.
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var ambiente = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{ambiente}.json", optional: true)
                .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration["DATABASE_URL"];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "String de conexão ausente. Configure os user secrets " +
                    "(dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\") " +
                    "ou a variável ConnectionStrings__DefaultConnection.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(ConnectionStringHelper.Normalizar(connectionString));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
