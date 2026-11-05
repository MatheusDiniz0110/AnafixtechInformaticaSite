// ---------- Importações ----------
using AnafixtechInformaticaSite.Data;                  // AppDbContext (banco)
using AnafixtechInformaticaSite.Models;                // User, LoginViewModel, RegisterViewModel
using AnafixtechInformaticaSite.Services;              // TokenService e PasswordHasher
using Microsoft.AspNetCore.Mvc;                        // Controller, IActionResult...
using Microsoft.EntityFrameworkCore;                   // FirstOrDefaultAsync
using System.Collections.Concurrent;                   // ConcurrentDictionary (controle de tentativas)

namespace AnafixtechInformaticaSite.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AccountController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // =========================================================================================
        // PROTEÇÃO CONTRA FORÇA BRUTA NO LOGIN
        // -----------------------------------------------------------------------------------------
        // Guarda, em memória, quantas vezes cada combinação "e-mail + IP" errou a senha.
        // Depois de 5 erros em 10 minutos, o acesso fica bloqueado por 15 minutos.
        //
        // Observação para estudo: isso funciona bem em um servidor único (o caso da AnaFixTech).
        // Se um dia houver vários servidores, o ideal é guardar esse controle no banco ou em Redis.
        // =========================================================================================
        private static readonly ConcurrentDictionary<string, TentativaLogin> _tentativas = new();
        private static readonly object _travaTentativas = new();

        private const int MaximoFalhas = 5;                                    // erros permitidos na janela
        private static readonly TimeSpan Janela = TimeSpan.FromMinutes(10);    // período de contagem
        private static readonly TimeSpan DuracaoBloqueio = TimeSpan.FromMinutes(15);

        // Classe interna que representa o estado de tentativas de uma chave.
        private sealed class TentativaLogin
        {
            public int Falhas { get; set; }
            public DateTime Inicio { get; set; } = DateTime.UtcNow;
            public DateTime? BloqueadoAte { get; set; }
        }

        // Monta a chave do controle: e-mail normalizado + IP de quem está tentando.
        private string ChaveTentativa(string email)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
            return $"{email.Trim().ToLowerInvariant()}|{ip}";
        }

        // Devolve true quando a chave está bloqueada e informa quantos minutos faltam.
        private static bool EstaBloqueado(string chave, out int minutosRestantes)
        {
            minutosRestantes = 0;

            lock (_travaTentativas)
            {
                if (!_tentativas.TryGetValue(chave, out var estado)) return false;

                // Bloqueio vencido: libera e zera a contagem.
                if (estado.BloqueadoAte.HasValue && estado.BloqueadoAte.Value <= DateTime.UtcNow)
                {
                    _tentativas.TryRemove(chave, out _);
                    return false;
                }

                if (estado.BloqueadoAte.HasValue)
                {
                    minutosRestantes = Math.Max(1, (int)Math.Ceiling((estado.BloqueadoAte.Value - DateTime.UtcNow).TotalMinutes));
                    return true;
                }
            }

            return false;
        }

        // Soma uma falha. Ao atingir o limite, aplica o bloqueio.
        private static void RegistrarFalha(string chave)
        {
            lock (_travaTentativas)
            {
                // Limpeza simples para o dicionário não crescer para sempre.
                if (_tentativas.Count > 5000)
                {
                    var limite = DateTime.UtcNow - Janela;
                    foreach (var par in _tentativas)
                    {
                        if (par.Value.Inicio < limite && par.Value.BloqueadoAte is null)
                            _tentativas.TryRemove(par.Key, out _);
                    }
                }

                var estado = _tentativas.GetOrAdd(chave, _ => new TentativaLogin());

                // Se a janela expirou, recomeça a contagem.
                if (DateTime.UtcNow - estado.Inicio > Janela)
                {
                    estado.Falhas = 0;
                    estado.Inicio = DateTime.UtcNow;
                    estado.BloqueadoAte = null;
                }

                estado.Falhas++;

                if (estado.Falhas >= MaximoFalhas)
                    estado.BloqueadoAte = DateTime.UtcNow + DuracaoBloqueio;
            }
        }

        // Login correto: apaga o histórico de erros daquela chave.
        private static void LimparTentativas(string chave)
        {
            lock (_travaTentativas)
            {
                _tentativas.TryRemove(chave, out _);
            }
        }

        // =========================================================================================
        // LOGIN
        // =========================================================================================

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var email = (model.Email ?? string.Empty).Trim().ToLowerInvariant();
            var chave = ChaveTentativa(email);

            // Antes de qualquer consulta: a origem está bloqueada por erros demais?
            if (EstaBloqueado(chave, out var minutos))
            {
                ModelState.AddModelError(string.Empty,
                    $"Muitas tentativas inválidas. Aguarde {minutos} minuto(s) e tente novamente.");
                return View(model);
            }

            // Procura a conta pelo e-mail (comparação sem diferenciar maiúsculas/minúsculas).
            var usuario = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            // Mensagem ÚNICA para e-mail inexistente e senha errada:
            // assim ninguém descobre quais e-mails têm conta no site.
            const string mensagemGenerica = "E-mail ou senha inválidos.";

            if (usuario is null)
            {
                RegistrarFalha(chave);
                ModelState.AddModelError(string.Empty, mensagemGenerica);
                return View(model);
            }

            // Conta desativada não entra, mesmo com a senha certa.
            if (!usuario.isActive)
            {
                RegistrarFalha(chave);
                ModelState.AddModelError(string.Empty, mensagemGenerica);
                return View(model);
            }

            // ---------- Conferência da senha ----------
            bool senhaCorreta;

            if (PasswordHasher.EhHash(usuario.Password))
            {
                // Conta nova: compara com o hash.
                senhaCorreta = PasswordHasher.Verificar(model.Password, usuario.Password);
            }
            else
            {
                // Conta antiga: a senha ainda está em texto puro.
                // Compara como antes e, se acertou, converte para hash na hora (migração automática).
                senhaCorreta = string.Equals(usuario.Password, model.Password, StringComparison.Ordinal);

                if (senhaCorreta)
                {
                    usuario.Password = PasswordHasher.Hash(model.Password);
                    await _context.SaveChangesAsync();
                }
            }

            if (!senhaCorreta)
            {
                RegistrarFalha(chave);
                ModelState.AddModelError(string.Empty, mensagemGenerica);
                return View(model);
            }

            // Deu certo: zera o contador de erros.
            LimparTentativas(chave);

            // A chave JWT vem da configuração (user secrets em desenvolvimento ou
            // variável de ambiente Jwt__SecretKey em produção). Nunca do código-fonte.
            var chaveJwt = _config["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(chaveJwt))
            {
                ModelState.AddModelError(string.Empty,
                    "Configuração de segurança ausente. Avise o administrador do site.");
                return View(model);
            }

            var token = TokenService.GenerateToken(usuario.Id.ToString(), usuario.Name, usuario.isAdmin, chaveJwt);

            // O token vai em um cookie HttpOnly: o JavaScript da página não consegue lê-lo,
            // o que reduz o estrago em caso de XSS. Secure = só trafega em HTTPS.
            Response.Cookies.Append("access_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(2)
            });

            // Administrador cai direto na dashboard; cliente comum volta para a home.
            return usuario.isAdmin
                ? RedirectToAction("Index", "Dashboard")
                : RedirectToAction("Index", "Home");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("access_token");
            return RedirectToAction("Index", "Home");
        }

        // =========================================================================================
        // CADASTRO
        // =========================================================================================

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var email = model.Email.Trim().ToLowerInvariant();

            // E-mail já usado?
            var emailExistente = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (emailExistente != null)
            {
                ModelState.AddModelError(string.Empty, "Este e-mail já está cadastrado.");
                return View(model);
            }

            // Nome de usuário já usado?
            var usuarioExistente = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == model.Username.Trim().ToLower());
            if (usuarioExistente != null)
            {
                ModelState.AddModelError(string.Empty, "Este nome de usuário já está em uso.");
                return View(model);
            }

            var novoUsuario = new User
            {
                Username = model.Username.Trim(),
                Email = email,
                Name = model.Name.Trim(),

                // A senha NUNCA é gravada como o usuário digitou: vai como hash.
                Password = PasswordHasher.Hash(model.Password),

                // Estes dois campos NÃO vêm do formulário (ver RegisterViewModel).
                // Quem se cadastra pelo site é sempre cliente comum e conta ativa.
                isAdmin = false,
                isActive = true
            };

            _context.Users.Add(novoUsuario);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login", "Account");
        }
    }
}
