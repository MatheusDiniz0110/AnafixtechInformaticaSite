using System.ComponentModel.DataAnnotations;

namespace AnafixtechInformaticaSite.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "O campo 'Email' é obrigatório.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "O campo 'Password' é obrigatório.")]
        public string Password { get; set; }
    }
}
