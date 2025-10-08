using System;
using System.ComponentModel.DataAnnotations;
using SalesAnalysis.Models;

namespace SalesAnalysis.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        public string ProductId { get; set; }

        public string CustomerId { get; set; }

        public DateTime DateOfSale { get; set; }

        public int QuantitySold { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal Discount { get; set; }

        public decimal ShippingCost { get; set; }

        public string PaymentMethod { get; set; }

        // Navigation properties
        public Product Product { get; set; }
        public Customer Customer { get; set; }
    }
}
