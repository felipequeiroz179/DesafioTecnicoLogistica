using DeliverySystem.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeliverySystem.OrderApi.Data;

public class DeliveryDbContextFactory : IDesignTimeDbContextFactory<DeliveryDbContext>
{
    public DeliveryDbContext CreateDbContext(string[] args)
    {
        // 1. Configurações
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        var builder = new DbContextOptionsBuilder<DeliveryDbContext>();

        var serverVersion = new MySqlServerVersion(new Version(8, 0, 29));
        builder.UseMySql(connectionString, serverVersion,
           
            options => options.EnableRetryOnFailure());

        return new DeliveryDbContext(builder.Options);
    }
}