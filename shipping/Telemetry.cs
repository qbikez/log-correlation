

using System.Diagnostics;

using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

public class EventGridDependencyInitializer : ITelemetryInitializer
{
    public const string EventTraceparentKey = "event_traceparent";
    public EventGridDependencyInitializer() { }
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is DependencyTelemetry dependency)
        {
            var activity = Activity.Current!;
            var id = activity.GetBaggageItem(EventTraceparentKey);
            if (!string.IsNullOrEmpty(id))
            {
                // the SpanId in request's traceparent becomes parent of the request
                // this will make AppInsights think that handler request is child of this dependency call
                dependency.Id = ActivityContext.Parse(id, null).SpanId.ToString();
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
        var currentSpanId = activity.SpanId.ToHexString();
        var traceparent = activity.Id.Replace(currentSpanId, nextSpanId);

        activity.AddBaggage(EventGridDependencyInitializer.EventTraceparentKey, traceparent);
        activity.AddBaggage("orignalspanid", currentSpanId);

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