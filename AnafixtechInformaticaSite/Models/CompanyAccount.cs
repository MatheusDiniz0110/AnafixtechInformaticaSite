using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnafixtechInformaticaSite.Models
{
    // Conta da empresa: assinatura, rede social, hospedagem, domínio, e-mail...
    // Guarda também o login e a senha de acesso ao serviço (a senha SEMPRE criptografada).
    [Table("CompanyAccounts")]
    public class CompanyAccount
    {
        [Key]
        public int Id { get; set; } // chave primária

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty; // nome da conta (ex.: "Instagram da empresa")

        [Required, StringLength(60)]
        public string Kind { get; set; } = "Assinatura"; // tipo de gasto (assinatura, hospedagem...)

        [StringLength(120)]
        public string? AppService { get; set; } // app ou serviço usado (ex.: "Canva", "Meta")

        [StringLength(500)]
        public string? Description { get; set; } // descrição livre do gasto

        public decimal? Price { get; set; } // preço da assinatura; nulo = conta sem custo

        public BillingCycle BillingCycle { get; set; } = BillingCycle.Mensal; // com que frequência é cobrada

        [StringLength(160)]
        public string? LoginEmail { get; set; } // e-mail usado no login do serviço

        [StringLength(120)]
        public string? LoginUser { get; set; } // usuário de login (quando não é e-mail)

        // Senha JÁ CRIPTOGRAFADA (AES-GCM) pelo AccountSecretProtector.
        // Aqui nunca entra texto puro. O tamanho extra acomoda versão + nonce + tag + dados.
        [StringLength(1000)]
        public string? ProtectedPassword { get; set; }

        [StringLength(250)]
        public string? Url { get; set; } // endereço do site/app para abrir rapidinho

        [StringLength(500)]
        public string? Notes { get; set; } // observações (plano contratado, vencimento...)

        public bool IsActive { get; set; } = true; // conta ainda em uso?

        public DateTime CreatedAt { get; set; } // quando o registro foi criado
        public DateTime UpdatedAt { get; set; } // última alteração
    }
}
