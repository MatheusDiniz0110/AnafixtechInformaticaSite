using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnafixtechInformaticaSite.Models
{
    // Peça do estoque (memória, SSD, fonte...). Corresponde ao "StockItem" do Base44.
    [Table("StockItems")]
    public class StockItem
    {
        [Key]
        public int Id { get; set; } // chave primária

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty; // nome da peça

        [Required, StringLength(40)]
        public string Category { get; set; } = "outro"; // categoria (ver DashboardLists.CategoriasEstoque)

        public int Quantity { get; set; } // quantidade disponível agora

        public int MinQuantity { get; set; } // abaixo (ou igual) disso o estoque é considerado baixo

        public decimal? PurchasePrice { get; set; } // preço de compra unitário (opcional)

        [StringLength(120)]
        public string? Supplier { get; set; } // fornecedor

        [StringLength(120)]
        public string? Location { get; set; } // onde a peça fica guardada

        public DateOnly? LastPurchaseDate { get; set; } // data da última compra

        [StringLength(500)]
        public string? Description { get; set; } // detalhes da peça

        // Nível calculado na hora (não vai para o banco): zero = crítico; até o mínimo = baixo; senão OK.
        [NotMapped]
        public StockLevel Level => Quantity <= 0 ? StockLevel.Critical
                                 : Quantity <= MinQuantity ? StockLevel.Low
                                 : StockLevel.Ok;
    }
}
