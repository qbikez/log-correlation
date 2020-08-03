using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace shipping
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry(opts =>
            {
                opts.EnablePerformanceCounterCollectionModule = false;
                opts.AddAutoCollectedMetricExtractor = false;
            });
            services.AddSingleton<ITelemetryInitializer, MyTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer>(new CloudRoleNameTelemetryInitializer("Shipping"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory, IConfiguration config)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.Use(async(context, next) =>
            {
                Activity activity = null;
                if (context.Request.Headers.ContainsKey("MyOperationId"))
                {
                    var operationId = context.Request.Headers["MyOperationId"].ToString();

                    activity = new Activity(Activity.Current.OperationName);
                    activity.SetIdFormat(ActivityIdFormat.W3C);
                    activity.SetParentId(operationId);

                    activity.Start();
                    context.Items["Activity"] = activity;
                }
                try
                {
                    await next();
                }
                finally
                {
                    activity?.Stop();
                }
            });
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/events", async context =>
                {
                    var handler = new EventGridHandler(loggerFactory.CreateLogger<EventGridHandler>());
                    await handler.Handle(context, async gridEvent =>
                    {
                        var logger = loggerFactory.CreateLogger<Startup>();
                        logger.LogInformation($"processing grid event: {JsonConvert.SerializeObject(gridEvent)}");
                        if (gridEvent.EventType == "WarehouseDepleted")
                        {
                            logger.LogInformation("sending another event");
                            using var eventGrid = new EventGridClient(new TopicCredentials(config["EventGrid:Key"]));
                            await eventGrid.PublishEventsAsync(config["EventGrid:Hostname"], new List<EventGridEvent>()
                            {
                                new EventGridEvent()
                                {
                                    Id = Guid.NewGuid().ToString(),
                                        Topic = "shipping",
                                        Data = JObject.FromObject(new
                                        {
                                            traceparent = Activity.Current.Id,                                                
                                        }),
                                        EventType = "ItemShipped",
                                        Subject = $"shipping",
                                        DataVersion = "1.0.1"
                                }
                            });
                        }

                        await Task.Yield();
                    });
                });
                endpoints.MapGet("/echo", async context =>
                {
                    var headers = context.Request.Headers;
                    var activity = new
                    {
                        RootId = System.Diagnostics.Activity.Current.RootId,
                        Id = System.Diagnostics.Activity.Current.Id,
                        ParentId = System.Diagnostics.Activity.Current.ParentId,
                        ParentSpanId = System.Diagnostics.Activity.Current.ParentSpanId,
                        SpanId = System.Diagnostics.Activity.Current.SpanId,
                        TraceId = System.Diagnostics.Activity.Current.TraceId,
                    };

                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                    {
                        activity,
                        headers
                    }));
                });
            });
        }
    }

    public class CloudRoleNameTelemetryInitializer : ITelemetryInitializer
    {
        public readonly string roleName;
        public CloudRoleNameTelemetryInitializer(string roleName)
        {
            this.roleName = roleName;
        }
        public void Initialize(ITelemetry telemetry)
        {
            // set custom role name here
            telemetry.Context.Cloud.RoleName = this.roleName;
        }
    }
}