

using System.Diagnostics;

using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

public class EventGridDependencyInitializer : ITelemetryInitializer
{
    public const string NextSpanIdProperty = "next_spanId";
    public EventGridDependencyInitializer() { }
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is DependencyTelemetry dependency)
        {
            var activity = Activity.Current!;
            // next_spanId is the spanId included in traceparent field inside the event
            // this will make AppInsights think that handler request is child of this dependency call
            var id = activity.GetBaggageItem(NextSpanIdProperty);
            if (!string.IsNullOrEmpty(id))
            {
                dependency.Id = id;
                // "Azure Service Bus" is nicely interpreted in AppInsights graph
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
        activity.AddBaggage(EventGridDependencyInitializer.NextSpanIdProperty, nextSpanId);

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