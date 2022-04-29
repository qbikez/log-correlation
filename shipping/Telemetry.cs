

using System.Diagnostics;
using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace shipping;

public class EventGridDependencyInitializer : ITelemetryInitializer
{
    public EventGridDependencyInitializer() { }
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is DependencyTelemetry dependency)
        {
            var activity = Activity.Current!;
            var id = activity.GetBaggageItem("next_spanId");
            if (!string.IsNullOrEmpty(id))
            {
                dependency.Id = id;
                dependency.Type = "Azure Service Bus";
            }
        }

    }
}

public static class ActivityExtensions
{
    public static string TraceParent(this Activity activity)
    {
        if (activity?.SpanId == null || activity?.Id == null) return null;

        var nextSpanId = ActivitySpanId.CreateRandom().ToHexString();
        activity.AddBaggage("next_spanId", nextSpanId);

        var currentSpanId = activity.SpanId.ToHexString();
        var traceparent = activity.Id.Replace(currentSpanId, nextSpanId);

        return traceparent;
    }
}

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