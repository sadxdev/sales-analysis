using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesAnalysis.Models
{
    public class Customer
    {
        [Key]
        public string CustomerId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; }

        [MaxLength(100)]
        public string CustomerEmail { get; set; }

        [MaxLength(200)]
        public string CustomerAddress { get; set; }

        // Navigation property
        public ICollection<Order> Orders { get; set; }
    }
}
