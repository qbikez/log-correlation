# log-correlation

A toy project to test Application Insights logs correlation.

## How to

[How to add correlation to EventGrid events](HowTo.md)

## This code

#### orders

User-facing service that accepts orders. 

#### warehouse

Backend service that oversees the number of available items.

#### shipping

Ships ordered items.

### Flow

```mermaid
sequenceDiagram
    client->>orders: POST /order/
    orders->>warehouse: GET /items/
    Note over warehouse: [WarehouseDepleted]
    warehouse->>orders: 200 OK
    orders->>client: 200 OK
    Note over orders: [OrderAccepted]
    warehouse-->>shipping: [WarehouseDepleted]
    Note over shipping: [ItemShipped]
```

