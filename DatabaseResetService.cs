using Microsoft.EntityFrameworkCore;
using Rxnxt.Business.Data;

namespace PharmacySalesApp
{
    public class DatabaseResetService
    {
        public static void ResetAndSeedDatabase(PharmacyDbContext context)
        {
            // Delete all existing data
            context.SaleItems.RemoveRange(context.SaleItems);
            context.Sales.RemoveRange(context.Sales);
            context.Payments.RemoveRange(context.Payments);
            context.Batches.RemoveRange(context.Batches);
            context.Medicines.RemoveRange(context.Medicines);
            context.Customers.RemoveRange(context.Customers);
            
            context.SaveChanges();
            
            Console.WriteLine("Existing data cleared successfully!");
            
            // Seed fresh demo data
            DemoDataSeeder.SeedData(context);
        }
    }
}
