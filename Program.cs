using System.Text.Json;
using Application.Services;
using Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
              .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient<IOpenAiMappingService, OpenAiMappingService>();
        services.AddSingleton<IDynamicsPayloadValidator, DynamicsPayloadValidator>();
        services.AddSingleton<IPayloadLoggingService, PayloadLoggingService>();

        services.Configure<JsonSerializerOptions>(options =>
        {
            // Dynamics fields are explicit lowercase/custom names; keep property names untouched.
            options.PropertyNamingPolicy = null;
            options.PropertyNameCaseInsensitive = true;
            options.WriteIndented = false;
        });
    })
    .Build();

await host.RunAsync();
