using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesAnalysis.Models
{
    public class Product
    {
        [Key]
        public string ProductId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ProductName { get; set; }

        [MaxLength(50)]
        public string Category { get; set; }

        // Navigation property
        public ICollection<Order> Orders { get; set; }
    }
}
