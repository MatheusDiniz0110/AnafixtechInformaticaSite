using System.ComponentModel.DataAnnotations;

namespace AnafixtechInformaticaSite.Models
{
    public class RegisterViewModel
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
        public string? Devices { get; set; }
        public bool isAdmin { get; set; } = false;
        public bool isActive { get; set; } = true;
    }
}