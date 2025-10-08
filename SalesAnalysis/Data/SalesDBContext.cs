using Microsoft.EntityFrameworkCore;

namespace SalesAnalysis.Data
{
    public class SalesDbContext : DbContext
    {
        public SalesDbContext(DbContextOptions<SalesDbContext> options) : base(options) { }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<RefreshLog> RefreshLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().HasKey(p => p.Code);
            modelBuilder.Entity<Customer>().HasKey(c => c.Code);
            modelBuilder.Entity<Order>().HasKey(o => o.Code);

            modelBuilder.Entity<Category>().HasIndex(c => c.Name).IsUnique();

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderCode);

            modelBuilder.Entity<OrderItem>().HasIndex(oi => oi.ProductCode);
            modelBuilder.Entity<Order>().HasIndex(o => o.DateOfSale);
            modelBuilder.Entity<Order>().HasIndex(o => o.Region);
        }
    }
}
