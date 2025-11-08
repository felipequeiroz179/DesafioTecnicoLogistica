using DeliverySystem.Core.Data;
using DeliverySystem.OrderProcessor;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Serilog;

IHost host = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, config) =>
    {
        config.ReadFrom.Configuration(context.Configuration);
    })
    .ConfigureServices((hostContext, services) =>
    {
       
        var connString = hostContext.Configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<DeliveryDbContext>(options =>
            options.UseMySql(connString, ServerVersion.AutoDetect(connString),
                
                mysqlOptions => mysqlOptions.EnableRetryOnFailure()));

     
        services.AddSingleton<IConnection>(sp =>
        {
            var config = hostContext.Configuration.GetSection("RabbitMQ");
            var factory = new ConnectionFactory
            {
                HostName = config["HostName"]
            };

            
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();