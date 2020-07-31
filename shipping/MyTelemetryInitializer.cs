using System.Diagnostics;
using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace shipping
{
    class MyTelemetryInitializer : TelemetryInitializerBase
    {
        public MyTelemetryInitializer(IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
        { }

        protected override void OnInitializeTelemetry(HttpContext platformContext, RequestTelemetry requestTelemetry, ITelemetry telemetry)
        {
            if (platformContext.Items.TryGetValue("OperationId", out var operationIdItem)) {
                var operationId = operationIdItem.ToString();
                requestTelemetry.Context.Operation.Id = operationId as string;
                requestTelemetry.Context.Operation.ParentId = operationId as string;
            }
        }
    }
}