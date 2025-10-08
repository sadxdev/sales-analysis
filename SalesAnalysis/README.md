# Sales Analytics Backend

A backend solution for analyzing sales data from large CSV files using **C# / ASP.NET Core**, **PostgreSQL**, and **Redis** for caching. This project supports:

- Loading large CSV files into a normalized database schema.
- Daily or on-demand data refresh.
- RESTful APIs for revenue calculations:
  - Total revenue
  - Revenue by product
  - Revenue by category
  - Revenue by region
  - Revenue trends over time
- Caching frequently requested queries using Redis.

---

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Setup](#setup)
- [Database Schema](#database-schema)
- [APIs](#apis)
- [Data Refresh](#data-refresh)
- [Logging](#logging)
- [License](#license)

---

## Features

1. **Data Loading**

   - Efficiently load large CSV files into PostgreSQL.
   - Validates and normalizes orders, products, categories, and customers.

2. **Data Refresh**

   - Daily automatic refresh or on-demand refresh via API.
   - Handles duplicates and logs success/failure.

3. **Revenue Calculations**

   - Total revenue within a date range.
   - Revenue by product/category/region.
   - Revenue trends (monthly/quarterly/yearly).

4. **Caching**
   - Redis used to cache frequent queries (30 min TTL).

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- [Redis 7+](https://redis.io/download)
- (Optional) Docker & Docker Compose for containerized setup.

---

## Setup

1. **Clone Repository**

```bash
git clone https://github.com/sadxdev/sales-analysis.git
cd sales-analytics-backend

Configure appsettings.json

{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=salesdb;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  },
  "DailyRefresh": {
    "FilePath": "data/sales.csv",
    "TimeOfDay": "02:00:00"
  }
}


Restore Packages

dotnet restore


Run Database Migrations

dotnet ef database update


Run the API

dotnet run --project SalesAnalysis.API


API will be available at https://localhost:5001 (or http://localhost:5000).

Database Schema

The database is normalized:

Orders (Code, DateOfSale, ShippingCost, Region, ...)

OrderItems (OrderCode, ProductCode, Quantity, UnitPrice, Discount)

Products (Code, Name, CategoryId, UnitPrice, ...)

Categories (Id, Name)

Customers (Id, Name, Email, Address)

Schema Diagram: database/schema_diagram.png

APIs
Route	Method	Query Parameters	Description
/api/revenue/total	GET	startDate, endDate	Total revenue for a date range
/api/revenue/by-product	GET	startDate, endDate, top (optional, default 50)	Top N products by revenue
/api/revenue/by-category	GET	startDate, endDate	Revenue grouped by category
/api/revenue/by-region	GET	startDate, endDate	Revenue grouped by region
/api/revenue/trends	GET	startDate, endDate, period (monthly, quarterly, yearly)	Revenue trends over time
/api/refresh	POST	N/A	Trigger on-demand data refresh

Sample Request:

GET /api/revenue/total?startDate=2024-01-01&endDate=2024-06-30


Sample Response:

{
  "startDate": "2024-01-01T00:00:00+00:00",
  "endDate": "2024-06-30T00:00:00+00:00",
  "itemRevenue": 125000.50,
  "shipping": 4500.00,
  "total": 129500.50
}

Data Refresh

Automatic daily refresh: configured via DailyRefresh.TimeOfDay

Manual refresh: call POST /api/refresh

CSV file path configured via DailyRefresh.FilePath

Logging

Logs are written via ASP.NET Core logging.

Cached hits/misses and data refresh outcomes are logged.
```
