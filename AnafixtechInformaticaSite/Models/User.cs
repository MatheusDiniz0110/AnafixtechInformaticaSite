using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AnafixtechInformaticaSite.Models
{
    [Table("Users")]
    public class User
    {
        public User()
        {
            Username = string.Empty;
            Name = string.Empty;
            Email = string.Empty;
            Password = string.Empty;
            Devices = string.Empty;
            isAdmin = false;
            isActive = true;
        }
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "O nome de usuário é obrigatório")]
        public string Username { get; set; }
        [Required(ErrorMessage = "O nome é obrigatório")]
        public string Name { get; set; }
        [Required(ErrorMessage = "O e-mail é obrigatório")]
        public string Email { get; set; }
        [Required(ErrorMessage = "A senha é obrigatória")]
        public string Password { get; set; }

        public string? Devices { get; set; }
        public bool isAdmin { get; set; } = false;
        public bool isActive { get; set; } = true;
    }
}
