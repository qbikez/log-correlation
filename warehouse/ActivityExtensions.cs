using System;
using System.Diagnostics;

namespace warehouse
{
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
}