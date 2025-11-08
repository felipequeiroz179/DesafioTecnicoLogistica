using Microsoft.EntityFrameworkCore;
using DeliverySystem.Core.Data;

namespace DeliverySystem.OrderApi.Data;

public static class MigrationExtensions
{
    // Este método de extensão aplica as migrações pendentes
    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();

        try
        {
            dbContext.Database.Migrate();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao aplicar migrações: {ex.Message}");
        }
    }
}