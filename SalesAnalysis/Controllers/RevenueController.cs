using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesAnalysis.Data;
using StackExchange.Redis;
using System.Text.Json;

namespace SalesAnalysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RevenueController : ControllerBase
    {
        private readonly SalesDbContext _db;
        private readonly IDatabase _cache;
        private readonly ILogger<RevenueController> _logger;

        public RevenueController(SalesDbContext db, IConnectionMultiplexer redis, ILogger<RevenueController> logger)
        {
            _db = db;
            _cache = redis.GetDatabase();
            _logger = logger;
        }

        // helper: revenue = sum(quantity * unit_price * (1 - discount)) + sum(shipping cost)
        private static string CacheKey(string prefix, DateTimeOffset startDate, DateTimeOffset endDate) =>
            $"{prefix}:{startDate:yyyy-MM-dd}:{endDate:yyyy-MM-dd}";

        [HttpGet("total")]
        public async Task<IActionResult> TotalRevenue([FromQuery] DateTimeOffset startDate, [FromQuery] DateTimeOffset endDate)
        {
            if (startDate >= endDate) return BadRequest("`startDate` must be < `endDate`");

            var key = CacheKey("revenue:total", startDate, endDate);
            var cached = await _cache.StringGetAsync(key);
            if (cached.HasValue)
            {
                _logger.LogInformation("Cache hit {Key}", key);
                return Ok(JsonSerializer.Deserialize<object>(cached));
            }

            // compute revenue
            var itemsQuery = from oi in _db.OrderItems
                             join o in _db.Orders on oi.OrderCode equals o.Code
                             where o.DateOfSale >= startDate && o.DateOfSale <= endDate
                             select new { oi.Quantity, oi.UnitPrice, oi.Discount, o.ShippingCost };

            var itemRevenue = await itemsQuery
                         .Select(x => (x.Quantity * x.UnitPrice * (1 - x.Discount)))
                         .SumAsync();

            var shippingSum = await _db.Orders
                .Where(o => o.DateOfSale >= startDate && o.DateOfSale <= endDate)
                .Select(o => o.ShippingCost)
                .SumAsync();

            var total = itemRevenue + shippingSum;

            var result = new { startDate, endDate, itemRevenue = itemRevenue, shipping = shippingSum, total };
            await _cache.StringSetAsync(key, JsonSerializer.Serialize(result), TimeSpan.FromMinutes(30));

            return Ok(result);
        }

        [HttpGet("by-product")]
        public async Task<IActionResult> RevenueByProduct([FromQuery] DateTimeOffset startDate, [FromQuery] DateTimeOffset endDate, [FromQuery] int top = 50)
        {
            var key = $"{CacheKey("revenue:byproduct", startDate, endDate)}:top{top}";
            var cached = await _cache.StringGetAsync(key);
            if (cached.HasValue) return Ok(JsonSerializer.Deserialize<object>(cached));

            var q = from oi in _db.OrderItems
                    join o in _db.Orders on oi.OrderCode equals o.Code
                    where o.DateOfSale >= startDate && o.DateOfSale <= endDate
                    group new { oi } by oi.ProductCode into g
                    select new
                    {
                        ProductCode = g.Key,
                        Revenue = g.Sum(x => (x.oi.UnitPrice * x.oi.Quantity * (1 - x.oi.Discount)))
                    };

            var topProducts = await q.OrderByDescending(x => x.Revenue).Take(top).ToListAsync();
            await _cache.StringSetAsync(key, JsonSerializer.Serialize(topProducts), TimeSpan.FromMinutes(30));

            return Ok(topProducts);
        }

        [HttpGet("by-category")]
        public async Task<IActionResult> RevenueByCategory([FromQuery] DateTimeOffset startDate, [FromQuery] DateTimeOffset endDate)
        {
            var key = CacheKey("revenue:bycategory", startDate, endDate);
            var cached = await _cache.StringGetAsync(key);
            if (cached.HasValue) return Ok(JsonSerializer.Deserialize<object>(cached));

            var q = from oi in _db.OrderItems
                    join o in _db.Orders on oi.OrderCode equals o.Code
                    join p in _db.Products on oi.ProductCode equals p.Code
                    join c in _db.Categories on p.CategoryId equals c.Id into cat
                    from c in cat.DefaultIfEmpty()
                    where o.DateOfSale >= startDate && o.DateOfSale <= endDate
                    group new { oi, c } by (c != null ? c.Name : "Uncategorized") into g
                    select new
                    {
                        Category = g.Key,
                        Revenue = g.Sum(x => (x.oi.UnitPrice * x.oi.Quantity * (1 - x.oi.Discount)))
                    };

            var result = await q.OrderByDescending(x => x.Revenue).ToListAsync();
            await _cache.StringSetAsync(key, JsonSerializer.Serialize(result), TimeSpan.FromMinutes(30));

            return Ok(result);
        }

        [HttpGet("by-region")]
        public async Task<IActionResult> RevenueByRegion([FromQuery] DateTimeOffset startDate, [FromQuery] DateTimeOffset endDate)
        {
            var key = CacheKey("revenue:byregion", startDate, endDate);
            var cached = await _cache.StringGetAsync(key);
            if (cached.HasValue) return Ok(JsonSerializer.Deserialize<object>(cached));

            var itemQ = from oi in _db.OrderItems
                        join o in _db.Orders on oi.OrderCode equals o.Code
                        where o.DateOfSale >= startDate && o.DateOfSale <= endDate
                        group new { oi, o } by o.Region into g
                        select new { Region = g.Key, Revenue = g.Sum(x => (x.oi.UnitPrice * x.oi.Quantity * (1 - x.oi.Discount))) };

            var shippingQ = from o in _db.Orders
                            where o.DateOfSale >= startDate && o.DateOfSale <= endDate
                            group o by o.Region into g
                            select new { Region = g.Key, Shipping = g.Sum(x => x.ShippingCost) };

            var items = await itemQ.ToListAsync();
            var shippings = await shippingQ.ToListAsync();

            var dict = items.ToDictionary(x => x.Region ?? "Unknown", x => x.Revenue);
            foreach (var s in shippings)
            {
                var r = s.Region ?? "Unknown";
                dict[r] = dict.GetValueOrDefault(r, 0M) + s.Shipping;
            }

            var result = dict.Select(kvp => new { Region = kvp.Key, Revenue = kvp.Value }).OrderByDescending(x => x.Revenue);
            await _cache.StringSetAsync(key, JsonSerializer.Serialize(result), TimeSpan.FromMinutes(30));

            return Ok(result);
        }

        [HttpGet("trends")]
        public async Task<IActionResult> RevenueTrends([FromQuery] DateTimeOffset startDate, [FromQuery] DateTimeOffset endDate, [FromQuery] string period = "monthly")
        {
            var key = CacheKey($"revenue:trends:{period}", startDate, endDate);
            var cached = await _cache.StringGetAsync(key);
            if (cached.HasValue) return Ok(JsonSerializer.Deserialize<object>(cached));

            string trunc = period.ToLower() switch
            {
                "yearly" => "year",
                "quarterly" => "quarter",
                _ => "month"
            };

            var sql = $@"
                SELECT date_trunc('{trunc}', o.date_of_sale) as period, 
                       SUM(oi.unit_price * oi.quantity * (1 - oi.discount)) as item_revenue,
                       SUM(o.shipping_cost) as shipping
                FROM order_items oi
                JOIN orders o ON oi.order_code = o.code
                WHERE o.date_of_sale >= @p0 AND o.date_of_sale <= @p1
                GROUP BY date_trunc('{trunc}', o.date_of_sale)
                ORDER BY date_trunc('{trunc}', o.date_of_sale);
            ";

            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var p0 = cmd.CreateParameter(); p0.ParameterName = "p0"; p0.Value = startDate; cmd.Parameters.Add(p0);
            var p1 = cmd.CreateParameter(); p1.ParameterName = "p1"; p1.Value = endDate; cmd.Parameters.Add(p1);

            var list = new List<object>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var periodVal = reader.GetFieldValue<DateTimeOffset>(0);
                    var itemRev = reader.IsDBNull(1) ? 0M : reader.GetDecimal(1);
                    var shipping = reader.IsDBNull(2) ? 0M : reader.GetDecimal(2);
                    list.Add(new { period = periodVal, itemRevenue = itemRev, shipping, total = itemRev + shipping });
                }
            }

            await _cache.StringSetAsync(key, JsonSerializer.Serialize(list), TimeSpan.FromMinutes(30));
            return Ok(list);
        }
    }
}
