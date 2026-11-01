using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AnafixtechInformaticaSite.Models
{
    [Table("ServiceRequests")]
    public class ServiceRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public string ServiceId { get; set; }

        [Required]
        public string Device { get; set; }

        [Required]
        public string Description { get; set; }
            
        [Required]
        public double Price { get; set; }

        public RequestStatus Status { get; set; } = RequestStatus.InProgress;

        [Required]
        public DateTime RequestDate { get; set; }

        [Required]
        public DateTime? CompletionDate { get; set; }

        public enum RequestStatus 
        {
            InProgress,
            Completed,
            Cancelled,
            WaitingForApproval,
            WaitingForParts,
            WaitingForPayment,
            WaitingForPickUp
        }
    }
}
