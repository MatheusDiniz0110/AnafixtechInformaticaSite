using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace AnafixtechInformaticaSite.Models
{
    [Table("Services")]
    public class Service
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Class { get; set; }
        [Required]
        public string Title { get; set; }
        [Required]
        public string Description { get; set; }
        [Required]
        public Decimal Price { get; set; }
        [Required]
        public string Status { get; set; }
    }
}