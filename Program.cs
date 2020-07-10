using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace AutoAppointApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services
                    .AddHttpClient()
                    .AddSingleton<CourseApi>()
                    .AddSingleton<GrabOrder>()
                    .AddHostedService<AutoAppointService>();

                    services.AddOptions()
                        .Configure<BaseInfo>(context.Configuration.GetSection("BaseInfo"));
                })
                .Build()
                .Run();
        }
    }
}
