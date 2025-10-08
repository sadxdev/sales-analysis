-- categories
CREATE TABLE IF NOT EXISTS categories (
  id SERIAL PRIMARY KEY,
  name TEXT UNIQUE NOT NULL
);

-- products
CREATE TABLE IF NOT EXISTS products (
  code TEXT PRIMARY KEY, -- Product ID from CSV (eg. "P123")
  name TEXT NOT NULL,
  category_id INTEGER REFERENCES categories(id)
);

-- customers
CREATE TABLE IF NOT EXISTS customers (
  code TEXT PRIMARY KEY, -- Customer ID from CSV (eg. "C456")
  name TEXT,
  email TEXT,
  address TEXT
);

-- orders
CREATE TABLE IF NOT EXISTS orders (
  code TEXT PRIMARY KEY, -- Order ID from CSV (eg. "1001")
  customer_code TEXT REFERENCES customers(code),
  date_of_sale TIMESTAMP WITH TIME ZONE,
  region TEXT,
  shipping_cost NUMERIC(12,2),
  payment_method TEXT
);

-- order_items
CREATE TABLE IF NOT EXISTS order_items (
  id BIGSERIAL PRIMARY KEY,
  order_code TEXT REFERENCES orders(code) ON DELETE CASCADE,
  product_code TEXT REFERENCES products(code),
  quantity INTEGER NOT NULL,
  unit_price NUMERIC(12,2) NOT NULL,
  discount NUMERIC(5,4) DEFAULT 0 -- fraction e.g. 0.1
);

-- refresh logs
CREATE TABLE IF NOT EXISTS refresh_logs (
  id SERIAL PRIMARY KEY,
  started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  finished_at TIMESTAMPTZ,
  status TEXT,
  message TEXT
);

-- Indexes to support queries
CREATE INDEX IF NOT EXISTS idx_orders_date ON orders (date_of_sale);
CREATE INDEX IF NOT EXISTS idx_order_items_product ON order_items (product_code);
CREATE INDEX IF NOT EXISTS idx_products_category ON products (category_id);
CREATE INDEX IF NOT EXISTS idx_orders_region ON orders (region);
