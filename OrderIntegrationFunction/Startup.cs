using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

[assembly: FunctionsStartup(typeof(OrderIntegrationFunction.Startup))]

namespace OrderIntegrationFunction
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            base.ConfigureAppConfiguration(builder);

            var context = builder.GetContext();
            var environmentName = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "local";

            builder.ConfigurationBuilder
            .SetBasePath(context.ApplicationRootPath)
            //.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            //.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

            var basePath = context.ApplicationRootPath;

            builder.ConfigurationBuilder.SetBasePath(basePath);

            var appSettingsPath = Path.Combine(basePath, "appsettings.json");
            var envSettingsPath = Path.Combine(basePath, $"appsettings.{environmentName}.json");

            if (File.Exists(appSettingsPath))
            {
                //builder.ConfigurationBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            }

            if (File.Exists(envSettingsPath))
            {
                //builder.ConfigurationBuilder.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false);
            }

            builder.ConfigurationBuilder.AddEnvironmentVariables();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var context = builder.GetContext();

            var orderServiceBaseUrl = context.Configuration["OrderServiceBaseUrl"] ?? "https://localhost:5001";
            
            builder.Services.Configure<BlobSettings>(context.Configuration.GetSection("BlobSettings"));
            builder.Services.Configure<ServiceBusSettings>(context.Configuration.GetSection("ServiceBusSettings"));

            builder.Services.AddSingleton<IOrderBlobService, OrderBlobService>();
            builder.Services.AddSingleton<IOrderServiceBusService, OrderServiceBusService>();

            builder.Services.AddHttpClient("OrderService", client =>
            {
                client.BaseAddress = new Uri(orderServiceBaseUrl);
            });
        }
    }
}