using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SiteAssistenciaTecnica.Models
{
    [Table("HomePageInformation")]
    public class HomePageInformation
    {
        public HomePageInformation()
        {
            ServicesPerformed = 0;
            SatisfiedCustomers = 0;
            DeliveryTime = 0;
            SatisfactionIndex = 0;
        }
        [Key]
        public int Id { get; set; }
        [Required]
        public int ServicesPerformed { get; set; }
        [Required]
        public int SatisfiedCustomers { get; set; }
        [Required]
        public int DeliveryTime { get; set; }
        [Required]
        public int SatisfactionIndex { get; set; }
    }
}
