using AnafixtechInformaticaSite.Models;
using Microsoft.EntityFrameworkCore;


namespace AnafixtechInformaticaSite.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Uma única linha que representa a tabela inteira do banco de dados
        public DbSet<HomePageInformation> HomePageInformation { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Service> Services { get; set; }
    }
}
