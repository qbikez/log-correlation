using System.Diagnostics;
using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace shipping
{
    class MyTelemetryInitializer : TelemetryInitializerBase
    {
        public MyTelemetryInitializer(IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor) { }

        protected override void OnInitializeTelemetry(HttpContext platformContext, RequestTelemetry requestTelemetry, ITelemetry telemetry)
        {
            //if (platformContext.Items.TryGetValue("Activity", out var activityObj))
            //{
            //    var activity = (Activity)activityObj;
            //    var operation = requestTelemetry.Context.Operation;

            //    SetOperationId(operation, activity);
            //}
        }

       
    }
}