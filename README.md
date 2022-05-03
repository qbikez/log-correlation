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

# Notes

When searching app insights for operation id, the first app in chain (orders) shows only event grid dependency.

![orders-timeline.png](orders-timeline.png)

The shipping app shows event handler call, as well as the source request to orders.

![shipping-timeline.png](shipping-timeline.png)


Events + serviceBus
![shipping-timeline-events+bus.png](shipping-timeline-events+bus.png)



https://devblogs.microsoft.com/cse/2021/05/13/observability-for-event-stream-processing-with-azure-functions-event-hubs-and-application-insights/

## operation links

[populationg operation links](https://github.com/microsoft/ApplicationInsights-dotnet/blob/405fd6a9916956f2233520c8ab66110a1f9dcfbc/WEB/Src/DependencyCollector/DependencyCollector/Implementation/AzureSdk/AzureSdkDiagnosticsEventHandler.cs#L355)

[a bug?](https://github.com/Azure/azure-sdk-for-net/issues/18002)