using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using SalesAnalysis.Data;
using System.Globalization;

namespace SalesAnalytics.Services
{
    public class LoadResult
    {
        public bool Success { get; set; }
        public int OrdersInserted { get; set; }
        public int ItemsInserted { get; set; }
        public string Message { get; set; }
    }

    public class CsvLoaderService : ICsvLoaderService
    {
        private readonly SalesDbContext _db;
        private readonly ILogger<CsvLoaderService> _logger;
        private const int BatchSize = 2000;

        public CsvLoaderService(SalesDbContext db, ILogger<CsvLoaderService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<LoadResult> LoadCsvFileAsync(string filePath, CancellationToken ct = default)
        {
            var log = new RefreshLog { StartedAt = DateTimeOffset.UtcNow, Status = "Running", Message = $"Starting load of {filePath}" };
            _db.RefreshLogs.Add(log);
            await _db.SaveChangesAsync(ct);

            int ordersInserted = 0;
            int itemsInserted = 0;

            // caches for existing values to reduce DB roundtrips
            var existingCategories = await _db.Categories.ToDictionaryAsync(c => c.Name, c => c.Id, ct);
            var existingProducts = await _db.Products.Select(p => p.Code).ToListAsync(ct);
            var existingCustomers = await _db.Customers.Select(c => c.Code).ToListAsync(ct);
            var existingOrders = await _db.Orders.Select(o => o.Code).ToListAsync(ct);

            var productSet = new HashSet<string>(existingProducts);
            var customerSet = new HashSet<string>(existingCustomers);
            var orderSet = new HashSet<string>(existingOrders);

            try
            {
                using var reader = new StreamReader(filePath);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    BadDataFound = null,
                    MissingFieldFound = null,
                    IgnoreBlankLines = true,
                    TrimOptions = TrimOptions.Trim
                };

                using var csv = new CsvReader(reader, config);
                // map by header names
                var batchOrderItems = new List<OrderItem>();
                var batchOrders = new List<Order>();
                var batchProducts = new List<Product>();
                var batchCustomers = new List<Customer>();
                var batchCategories = new List<Category>();

                while (await csv.ReadAsync())
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var row = new
                        {
                            OrderId = csv.GetField("Order ID") ?? csv.GetField("OrderID") ?? csv.GetField("OrderId"),
                            ProductId = csv.GetField("Product ID") ?? csv.GetField("ProductID") ?? csv.GetField("ProductId"),
                            CustomerId = csv.GetField("Customer ID") ?? csv.GetField("CustomerID") ?? csv.GetField("CustomerId"),
                            ProductName = csv.GetField("Product Name"),
                            Category = csv.GetField("Category"),
                            Region = csv.GetField("Region"),
                            DateOfSale = csv.GetField("Date of Sale") ?? csv.GetField("DateOfSale"),
                            QuantitySold = csv.GetField("Quantity Sold") ?? csv.GetField("Quantity"),
                            UnitPrice = csv.GetField("Unit Price") ?? csv.GetField("UnitPrice"),
                            Discount = csv.GetField("Discount"),
                            ShippingCost = csv.GetField("Shipping Cost") ?? csv.GetField("ShippingCost"),
                            PaymentMethod = csv.GetField("Payment Method") ?? csv.GetField("PaymentMethod"),
                            CustomerName = csv.GetField("Customer Name") ?? csv.GetField("CustomerName"),
                            CustomerEmail = csv.GetField("Customer Email") ?? csv.GetField("CustomerEmail"),
                            CustomerAddress = csv.GetField("Customer Address") ?? csv.GetField("CustomerAddress")
                        };

                        // Basic validations
                        if (string.IsNullOrWhiteSpace(row.OrderId) || string.IsNullOrWhiteSpace(row.ProductId))
                        {
                            _logger.LogWarning("Skipping row due to missing OrderId or ProductId.");
                            continue;
                        }

                        if (!int.TryParse(row.QuantitySold, out var qty)) qty = 1;
                        if (!decimal.TryParse(row.UnitPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var unitPrice)) unitPrice = 0M;
                        if (!decimal.TryParse(row.Discount, NumberStyles.Any, CultureInfo.InvariantCulture, out var discount)) discount = 0M;
                        if (!decimal.TryParse(row.ShippingCost, NumberStyles.Any, CultureInfo.InvariantCulture, out var shippingCost)) shippingCost = 0M;
                        DateTimeOffset? dateOfSale = null;
                        if (DateTimeOffset.TryParse(row.DateOfSale, out var parsedDate)) dateOfSale = parsedDate;

                        // ensure category exists
                        int? categoryId = null;
                        if (!string.IsNullOrWhiteSpace(row.Category))
                        {
                            if (!existingCategories.TryGetValue(row.Category, out var cid))
                            {
                                var cat = new Category { Name = row.Category.Trim() };
                                _db.Categories.Add(cat);
                                await _db.SaveChangesAsync(ct); // small upsert; categories count low
                                existingCategories[cat.Name] = cat.Id;
                                cid = cat.Id;
                            }
                            categoryId = cid;
                        }

                        // product upsert (simple)
                        if (!productSet.Contains(row.ProductId))
                        {
                            var p = new Product
                            {
                                Code = row.ProductId,
                                Name = row.ProductName,
                                CategoryId = categoryId
                            };
                            _db.Products.Add(p);
                            productSet.Add(p.Code);
                        }

                        // customer upsert (simple)
                        if (!customerSet.Contains(row.CustomerId))
                        {
                            var c = new Customer
                            {
                                Code = row.CustomerId,
                                Name = row.CustomerName,
                                Email = row.CustomerEmail,
                                Address = row.CustomerAddress
                            };
                            _db.Customers.Add(c);
                            customerSet.Add(c.Code);
                        }

                        // order insert (if not exist)
                        if (!orderSet.Contains(row.OrderId))
                        {
                            var order = new Order
                            {
                                Code = row.OrderId,
                                CustomerCode = row.CustomerId,
                                DateOfSale = dateOfSale,
                                Region = row.Region,
                                ShippingCost = shippingCost,
                                PaymentMethod = row.PaymentMethod
                            };
                            _db.Orders.Add(order);
                            orderSet.Add(order.Code);
                            ordersInserted++;
                        }

                        // order item
                        var item = new OrderItem
                        {
                            OrderCode = row.OrderId,
                            ProductCode = row.ProductId,
                            Quantity = qty,
                            UnitPrice = unitPrice,
                            Discount = discount
                        };
                        _db.OrderItems.Add(item);
                        itemsInserted++;

                        // batch save
                        if ((ordersInserted + itemsInserted) % BatchSize == 0)
                        {
                            await _db.SaveChangesAsync(ct);
                            _logger.LogInformation("Saved batch: orders={Orders} items={Items}", ordersInserted, itemsInserted);
                        }
                    }
                    catch (Exception exRow)
                    {
                        _logger.LogError(exRow, "Error parsing/processing row. Skipping.");
                        continue;
                    }
                }

                // final save
                await _db.SaveChangesAsync(ct);

                log.Status = "Success";
                log.Message = $"Inserted orders: {ordersInserted}, items: {itemsInserted}";
                log.FinishedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);

                return new LoadResult
                {
                    Success = true,
                    OrdersInserted = ordersInserted,
                    ItemsInserted = itemsInserted,
                    Message = log.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load CSV failed.");
                log.Status = "Failed";
                log.Message = ex.Message;
                log.FinishedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                return new LoadResult { Success = false, Message = ex.Message };
            }
        }
    }
}
