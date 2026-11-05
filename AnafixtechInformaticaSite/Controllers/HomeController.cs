using AnafixtechInformaticaSite.Data;
using AnafixtechInformaticaSite.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace AnafixtechInformaticaSite.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        public HomeController(AppDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var data = await _context.HomePageInformation.FirstOrDefaultAsync();
            return View(data);
        }

        // A rota antiga /Home/Servicos não tinha view e devolvia erro 500.
        // Agora ela apenas redireciona para o catálogo verdadeiro (/Services),
        // mantendo links antigos funcionando e sem duplicar página.
        public IActionResult Servicos()
        {
            return RedirectToActionPermanent("Index", "Services");
        }

        public IActionResult Privacy() => View();

        public IActionResult Sobre()
        {
            return View();
        }

        public IActionResult Solicitar()
        {
            // O formulário de solicitação agora vive no ServiceRequestController.
            return RedirectToAction("Create", "ServiceRequest");
        }

        public IActionResult Forum()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
