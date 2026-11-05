using System.ComponentModel.DataAnnotations;

namespace AnafixtechInformaticaSite.Models
{
    // Dados do formulário público "Criar conta".
    // Importante: esta classe NÃO tem as propriedades "isAdmin" nem "isActive".
    // Se elas existissem aqui, alguém poderia enviá-las escondidas no POST e se tornar
    // administrador (ataque chamado "overposting"/mass assignment). Quem define esses
    // valores é o AccountController, sempre como cliente comum e conta ativa.
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Informe um nome de usuário.")]
        [StringLength(60, MinimumLength = 3, ErrorMessage = "O usuário deve ter entre 3 e 60 caracteres.")]
        [Display(Name = "Usuário")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe seu nome.")]
        [StringLength(120, ErrorMessage = "O nome pode ter no máximo 120 caracteres.")]
        [Display(Name = "Nome completo")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        [StringLength(160, ErrorMessage = "O e-mail pode ter no máximo 160 caracteres.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe uma senha.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "A senha deve ter pelo menos 8 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; } = string.Empty;
    }
}
