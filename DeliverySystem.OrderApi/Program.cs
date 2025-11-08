using DeliverySystem.Core.Data;
using DeliverySystem.OrderApi;
using DeliverySystem.OrderApi.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Serilog;


var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
});



var connString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<DeliveryDbContext>(options =>
    options.UseMySql(
        connString, 
        ServerVersion.AutoDetect(connString),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)
    )
);


var rabbitConfig = builder.Configuration.GetSection("RabbitMQ");
var factory = new ConnectionFactory() 
{ 
    HostName = rabbitConfig["HostName"]
};
var connection = await factory.CreateConnectionAsync();
builder.Services.AddSingleton(connection);


builder.Services.AddHostedService<OutboxRelayService>();


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
   
    app.ApplyMigrations();
}

app.UseHttpsRedirection();
app.UseAuthorization();


app.UseSerilogRequestLogging();

app.MapControllers();
app.Run();