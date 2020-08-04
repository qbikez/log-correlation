using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace warehouse
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddApplicationInsightsTelemetry(opts => {
                opts.EnablePerformanceCounterCollectionModule = false;
                opts.AddAutoCollectedMetricExtractor = false;
            });
            services.AddSingleton<ITelemetryInitializer>(new CloudRoleNameTelemetryInitializer("Warehouse"));
            services.AddSingleton<ITelemetryInitializer>(new EventGridDependencyInitializer());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
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

    public class EventGridDependencyInitializer : ITelemetryInitializer
    {
        public EventGridDependencyInitializer()
        {
        }
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is DependencyTelemetry) {
                var dependency = (telemetry as DependencyTelemetry);
                var activity = Activity.Current;
                var id = activity.GetBaggageItem("next_spanId");
                if (!string.IsNullOrEmpty(id)) {
                    dependency.Id = id;
                }
            }
            
        }
    }
}
