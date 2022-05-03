

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
            if (string.IsNullOrEmpty(dependency.Name)) return;
            var activity = Activity.Current!;
            var id = activity.GetBaggageItem(EventTraceparentKey);
            if (!string.IsNullOrEmpty(id))
            {
                if (!dependency.Properties.ContainsKey("original_id")) dependency.Properties.Add("original_id", dependency.Id);
                if (!dependency.Properties.ContainsKey("overriden_id")) dependency.Properties.Add("overriden_id", id);

                if (dependency.Name == "POST /api/events")
                {
                    // the SpanId in request's traceparent becomes parent of the request
                    // this will make AppInsights think that handler request is child of this dependency call
                    dependency.Id = ActivityContext.Parse(id, null).SpanId.ToString();
                    // "Azure Service Bus" is nicely interpreted in AppInsights graph
                    dependency.Type = "Azure Service Bus";
                }
            }
        }
        if (telemetry is RequestTelemetry request) {
            var activity = Activity.Current!;
            var id = activity.GetBaggageItem(EventTraceparentKey);
            if (!string.IsNullOrEmpty(id)) {
                var context = ActivityContext.Parse(id, null);
                //request.Context.Operation.Id = context.TraceId.ToString();
                //request.Context.Operation.ParentId = context.SpanId.ToString();
                request.Properties.Add("overriden_id", context.TraceId.ToString());
                request.Properties.Add("overriden_parentid", context.SpanId.ToString());
            }
            System.Console.WriteLine(request);
        }
    }
}

public static class ActivityExtensions
{
    public static string TraceParent(this Activity activity)
    {
        if (activity?.SpanId == null || activity?.Id == null) return null;

        // at this point, we don't know what the generated dependency id will be
        // so we generate a new id and set it as dependency id later
        var nextSpanId = ActivitySpanId.CreateRandom().ToHexString();
        var currentSpanId = activity.SpanId.ToHexString();
        var traceparent = activity.Id.Replace(currentSpanId, nextSpanId);

        activity.AddBaggage(EventGridDependencyInitializer.EventTraceparentKey, traceparent);
        activity.AddBaggage("originalspanid", currentSpanId);

        return traceparent;
    }

    public static string SetTraceParent(this Activity activity, string traceparent)
    {
        activity.AddBaggage(EventGridDependencyInitializer.EventTraceparentKey, traceparent);

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