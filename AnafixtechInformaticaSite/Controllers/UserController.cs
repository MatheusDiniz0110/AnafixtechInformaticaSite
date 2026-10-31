using AnafixtechInformaticaSite.Models;
using AnafixtechInformaticaSite.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnafixtechInformaticaSite.Controllers
{
    public class UserController : Controller
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public bool ChecarTokenNoHeader()
        {
            // 1. Busca o cabeçalho "Authorization"
            string authHeader = Request.Headers["Authorization"];

            // 2. Verifica se ele existe e começa com "Bearer "
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                // Extrai apenas o texto do token (remove o prefixo "Bearer ")
                string token = authHeader.Substring("Bearer ".Length).Trim();

                return true;
            }

            return false;
        }

        public async Task<User?> Get(string email, string password)
        {
            // User search by email and password, ignoring case for email
            return await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.Password == password);
        }
    }
}
