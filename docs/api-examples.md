# API Examples

Start the stack before running these examples:

```bash
docker compose up --build
```

Public application traffic normally enters through the gateway at
`http://localhost:5100`. Catalog and Order Swagger pages are exposed directly
for development and exploration.

## Correlation IDs

Clients may supply `X-Correlation-ID`. If omitted, the service generates a GUID.
Every response returns the effective value.

```bash
curl -i -H "X-Correlation-ID: portfolio-demo-001" http://localhost:5100/catalog/api/products
```

## Catalog

List products:

```bash
curl http://localhost:5100/catalog/api/products
```

Create a product:

```bash
curl -i -X POST http://localhost:5100/catalog/api/products \
  -H "Content-Type: application/json" \
  -d '{
    "name": "USB-C Dock",
    "description": "Docking station for a development workspace.",
    "price": 149.99,
    "stock": 8
  }'
```

Save the returned `id` for the order request.

## Orders

Create an order. Clients send only product IDs and quantities; Order obtains
the authoritative product name, price, and stock from Catalog.

```bash
curl -i -X POST http://localhost:5100/orders/api/orders \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: portfolio-demo-002" \
  -d '{
    "customerEmail": "customer@commerce.dev",
    "items": [
      {
        "productId": "REPLACE_WITH_A_CATALOG_PRODUCT_ID",
        "quantity": 1
      }
    ]
  }'
```

List orders:

```bash
curl http://localhost:5100/orders/api/orders
```

Update an order status:

```bash
curl -i -X PATCH http://localhost:5100/orders/api/orders/REPLACE_WITH_AN_ORDER_ID/status \
  -H "Content-Type: application/json" \
  -d '{ "status": "Confirmed" }'
```

## Problem Details

Validation and application errors use `application/problem+json`. For example,
an unavailable Catalog dependency causes Order creation to return HTTP 503 with
a generic Problem Details document; stack traces are never sent to clients.

## Health probes

```bash
curl http://localhost:5100/health/live
curl http://localhost:5100/health/ready
curl http://localhost:5101/health/ready
curl http://localhost:5102/health/ready
```

Each response contains the overall `status`, the `service` name, and individual
dependency statuses.
