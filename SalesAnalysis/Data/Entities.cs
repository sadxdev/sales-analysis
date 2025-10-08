using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesAnalysis.Data
{
    public class Category
    {
        public int Id { get; set; }
        [Required] public string Name { get; set; }
    }

    public class Product
    {
        [Key] public string Code { get; set; } // P123
        public string Name { get; set; }
        public int? CategoryId { get; set; }
        public Category Category { get; set; }
    }

    public class Customer
    {
        [Key] public string Code { get; set; } // C456
        public string Name { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
    }

    public class Order
    {
        [Key] public string Code { get; set; } // 1001
        public string CustomerCode { get; set; }
        public Customer Customer { get; set; }
        public DateTimeOffset? DateOfSale { get; set; }
        public string Region { get; set; }
        public decimal ShippingCost { get; set; }
        public string PaymentMethod { get; set; }

        public ICollection<OrderItem> Items { get; set; }
    }

    public class OrderItem
    {
        public long Id { get; set; }
        public string OrderCode { get; set; }
        public Order Order { get; set; }
        public string ProductCode { get; set; }
        public Product Product { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; } // fraction e.g. 0.1
    }

    public class RefreshLog
    {
        public int Id { get; set; }
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedAt { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }
}
