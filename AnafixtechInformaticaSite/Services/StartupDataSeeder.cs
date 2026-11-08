using AnafixtechInformaticaSite.Data;
using AnafixtechInformaticaSite.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace AnafixtechInformaticaSite.Services
{
    // Tarefas de inicialização que dependem do banco já migrado:
    //   1. preencher os dados de referência (serviços e números da home) em um banco novo;
    //   2. garantir a conta do administrador configurada no ambiente.
    //
    // Roda apenas quando Database__MigrateOnStartup=true, que é o caso da produção. Em
    // desenvolvimento o banco local já tem os dados e o admin pode ser promovido pelo SQL
    // documentado no DEPLOY.md.
    public static class StartupDataSeeder
    {
        // Executa o db/seed.sql embutido quando a tabela de serviços está vazia.
        // O script usa ON CONFLICT DO NOTHING, então rodar de novo não duplica nada.
        public static void SeedReferenceData(AppDbContext banco, ILogger logger)
        {
            if (banco.Services.Any())
            {
                return;
            }

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("AnafixtechInformaticaSite.Data.seed.sql");

            if (stream is null)
            {
                logger.LogWarning("seed.sql não encontrado no binário; os serviços não foram preenchidos.");
                return;
            }

            using var leitor = new StreamReader(stream);
            var sql = leitor.ReadToEnd();

            banco.Database.ExecuteSqlRaw(sql);
            logger.LogInformation("Dados iniciais aplicados: serviços do catálogo e números da home.");
        }

        // Cria (ou atualiza) a conta admin a partir de Admin:Email / Admin:Password.
        //
        // A senha é definida pelo ambiente (Admin__Password no Render), nunca no código ou no
        // Git. Se a conta já existir e a senha configurada for diferente, ela é redefinida —
        // o que também serve como recuperação de acesso.
        public static void EnsureAdmin(AppDbContext banco, IConfiguration configuracao, ILogger logger)
        {
            var email = (configuracao["Admin:Email"] ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            var senha = configuracao["Admin:Password"];
            var usuario = banco.Users.FirstOrDefault(u => u.Email.ToLower() == email);

            // Sem senha configurada: se a conta já existe (criada pelo cadastro do site),
            // apenas garante o perfil de administrador. Se não existe, não há o que fazer.
            if (string.IsNullOrWhiteSpace(senha))
            {
                if (usuario is null)
                {
                    logger.LogInformation(
                        "Admin:Email definido, mas Admin:Password não. Cadastre a conta em /Account/Register " +
                        "ou defina Admin__Password para criá-la automaticamente.");
                    return;
                }

                if (!usuario.isAdmin || !usuario.isActive)
                {
                    usuario.isAdmin = true;
                    usuario.isActive = true;
                    banco.SaveChanges();
                    logger.LogInformation("Conta {Email} promovida a administrador.", email);
                }

                return;
            }

            if (senha.Length < 8)
            {
                logger.LogWarning("Admin:Password tem menos de 8 caracteres; a conta admin não será alterada.");
                return;
            }

            if (usuario is null)
            {
                usuario = new User
                {
                    Username = GerarUsername(banco, configuracao["Admin:Username"], email),
                    Name = string.IsNullOrWhiteSpace(configuracao["Admin:Name"])
                        ? "Administrador"
                        : configuracao["Admin:Name"]!.Trim(),
                    Email = email,
                    Password = PasswordHasher.Hash(senha),
                    isAdmin = true,
                    isActive = true
                };

                banco.Users.Add(usuario);
                banco.SaveChanges();
                logger.LogInformation("Conta admin criada para {Email}.", email);
                return;
            }

            var alterou = false;

            if (!usuario.isAdmin)
            {
                usuario.isAdmin = true;
                alterou = true;
            }

            if (!usuario.isActive)
            {
                usuario.isActive = true;
                alterou = true;
            }

            // A senha do banco é um hash; só troca se a senha configurada não conferir.
            if (!PasswordHasher.Verificar(senha, usuario.Password))
            {
                usuario.Password = PasswordHasher.Hash(senha);
                alterou = true;
            }

            if (alterou)
            {
                banco.SaveChanges();
                logger.LogInformation("Conta admin atualizada para {Email}.", email);
            }
        }

        // Usa Admin:Username se informado; senão deriva do e-mail. Evita colisão com
        // contas existentes acrescentando um número.
        private static string GerarUsername(AppDbContext banco, string? configurado, string email)
        {
            var baseNome = string.IsNullOrWhiteSpace(configurado)
                ? email.Split('@')[0]
                : configurado!.Trim();

            baseNome = new string(baseNome
                .Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')
                .ToArray())
                .Trim('.', '_', '-');

            if (baseNome.Length < 3)
            {
                baseNome = "admin";
            }

            if (baseNome.Length > 55)
            {
                baseNome = baseNome[..55];
            }

            var candidato = baseNome;
            var sufixo = 2;

            while (banco.Users.Any(u => u.Username.ToLower() == candidato.ToLower()))
            {
                candidato = $"{baseNome}{sufixo}";
                sufixo++;
            }

            return candidato;
        }
    }
}
