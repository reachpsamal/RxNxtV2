using Microsoft.EntityFrameworkCore;
using Rxnxt.Domain.Models;

namespace Rxnxt.Business.Data
{
    public class DemoDataSeeder
    {
        public static void SeedData(PharmacyDbContext context, bool forceReset = false)
        {
            // If forceReset is true, clear existing data
            if (forceReset)
            {
                // Delete in correct order to respect foreign key constraints
                context.SaleItems.RemoveRange(context.SaleItems);
                context.Payments.RemoveRange(context.Payments);
                context.Sales.RemoveRange(context.Sales);
                context.Batches.RemoveRange(context.Batches);
                context.Medicines.RemoveRange(context.Medicines);
                context.Customers.RemoveRange(context.Customers);
                context.SaveChanges();
                Console.WriteLine("Existing data cleared successfully!");
            }
            // Check if data already exists
            else if (context.Customers.Any() || context.Medicines.Any() || context.Batches.Any())
            {
                // Do not return here; seed missing sets independently.
            }

            // Seed Customers
            var customers = new List<Customer>
            {
                new Customer
                {
                    Name = "Rahul Sharma",
                    Phone = "9876543210",
                    // Email = "rahul.sharma@email.com",
                    // Address = "123, MG Road, Bangalore, Karnataka - 560001",
                    // DateOfBirth = new DateTime(1985, 5, 15),
                    // LoyaltyPoints = 250
                },
                new Customer
                {
                    Name = "Debasis singh",
                    Phone = "8456919551",
                    // LoyaltyPoints = 0
                },
                new Customer
                {
                    Name = "Rakesh Kumar",
                    Phone = "9556090334",
                    // LoyaltyPoints = 0
                },
                new Customer
                {
                    Name = "Subrat Pradhan",
                    Phone = "8895613251",
                    // LoyaltyPoints = 0
                },
                new Customer
                {
                    Name = "Binod Das",
                    Phone = "8280729150",
                    // LoyaltyPoints = 0
                }
            };

            var existingCustomerPhones = context.Customers
                .AsNoTracking()
                .Select(c => c.Phone)
                .ToHashSet();

            var newCustomers = customers
                .Where(c => !existingCustomerPhones.Contains(c.Phone))
                .ToList();

            if (newCustomers.Any())
            {
                context.Customers.AddRange(newCustomers);
                context.SaveChanges();
            }

            // Seed Medicines
            var medicines = new List<Medicine>
            {
                new Medicine
                {
                    Name = "Paracetamol 500mg",
                    GenericName = "Paracetamol",
                    Manufacturer = "Cipla Ltd",
                    Category = "Pain Relief"
                },
                new Medicine
                {
                    Name = "Azithromycin 250mg",
                    GenericName = "Azithromycin",
                    Manufacturer = "Pfizer Ltd",
                    Category = "Antibiotic"
                },
                new Medicine
                {
                    Name = "Amoxicillin 500mg",
                    GenericName = "Amoxicillin",
                    Manufacturer = "GlaxoSmithKline",
                    Category = "Antibiotic"
                },
                new Medicine
                {
                    Name = "Ibuprofen 400mg",
                    GenericName = "Ibuprofen",
                    Manufacturer = "Sun Pharma",
                    Category = "Pain Relief"
                },
                new Medicine
                {
                    Name = "Omeprazole 20mg",
                    GenericName = "Omeprazole",
                    Manufacturer = "Dr. Reddy's Laboratories",
                    Category = "Antacid"
                },
                new Medicine
                {
                    Name = "Metformin 500mg",
                    GenericName = "Metformin",
                    Manufacturer = "Lupin Ltd",
                    Category = "Anti-diabetic"
                },
                new Medicine
                {
                    Name = "Atorvastatin 10mg",
                    GenericName = "Atorvastatin",
                    Manufacturer = "Cadila Healthcare",
                    Category = "Cholesterol"
                },
                new Medicine
                {
                    Name = "Levothyroxine 50mcg",
                    GenericName = "Levothyroxine",
                    Manufacturer = "Abbott Healthcare",
                    Category = "Thyroid"
                },
                new Medicine
                {
                    Name = "Cetirizine 10mg",
                    GenericName = "Cetirizine",
                    Manufacturer = "Mankind Pharma",
                    Category = "Antihistamine"
                },
                new Medicine
                {
                    Name = "Vitamin D3 1000 IU",
                    GenericName = "Cholecalciferol",
                    Manufacturer = "Juggat Pharma",
                    Category = "Vitamins"
                }
            };

            // Seed medicines only if none exist
            if (!context.Medicines.Any())
            {
                context.Medicines.AddRange(medicines);
                context.SaveChanges();
            }

            // Map medicine name -> id (for batch seeding)
            var medicineIdByName = context.Medicines
                .AsNoTracking()
                .ToDictionary(m => m.Name, m => m.Id);

            int GetMedicineId(string medicineName)
            {
                if (!medicineIdByName.TryGetValue(medicineName, out var id))
                    throw new InvalidOperationException($"Demo seeding failed: Medicine not found: {medicineName}");
                return id;
            }

            // Seed Batches
            var batches = new List<Batch>
            {
                // Paracetamol batches
                new Batch
                {
                    MedicineId = GetMedicineId("Paracetamol 500mg"),
                    BatchNumber = "PAR2024001",
                    ExpiryDate = new DateTime(2025, 12, 31),
                    StripQuantity = 100,
                    TabletPerStrip = 10,
                    PurchasePrice = 15.00m,
                    SellingPriceStrip = 25.00m,
                    SellingPriceTablet = 2.50m
                },
                new Batch
                {
                    MedicineId = GetMedicineId("Paracetamol 500mg"),
                    BatchNumber = "PAR2024002",
                    ExpiryDate = new DateTime(2026, 6, 30),
                    StripQuantity = 150,
                    TabletPerStrip = 10,
                    PurchasePrice = 14.50m,
                    SellingPriceStrip = 24.00m,
                    SellingPriceTablet = 2.40m
                },
                // Azithromycin batches
                new Batch
                {
                    MedicineId = GetMedicineId("Azithromycin 250mg"),
                    BatchNumber = "AZI2024001",
                    ExpiryDate = new DateTime(2025, 8, 15),
                    StripQuantity = 50,
                    TabletPerStrip = 6,
                    PurchasePrice = 45.00m,
                    SellingPriceStrip = 75.00m,
                    SellingPriceTablet = 12.50m
                },
                new Batch
                {
                    MedicineId = GetMedicineId("Azithromycin 250mg"),
                    BatchNumber = "AZI2024002",
                    ExpiryDate = new DateTime(2026, 2, 28),
                    StripQuantity = 75,
                    TabletPerStrip = 6,
                    PurchasePrice = 43.00m,
                    SellingPriceStrip = 72.00m,
                    SellingPriceTablet = 12.00m
                },
                // Amoxicillin batches
                new Batch
                {
                    MedicineId = GetMedicineId("Amoxicillin 500mg"),
                    BatchNumber = "AMX2024001",
                    ExpiryDate = new DateTime(2025, 10, 20),
                    StripQuantity = 80,
                    TabletPerStrip = 10,
                    PurchasePrice = 35.00m,
                    SellingPriceStrip = 55.00m,
                    SellingPriceTablet = 5.50m
                },
                // Ibuprofen batches
                new Batch
                {
                    MedicineId = GetMedicineId("Ibuprofen 400mg"),
                    BatchNumber = "IBU2024001",
                    ExpiryDate = new DateTime(2025, 11, 30),
                    StripQuantity = 120,
                    TabletPerStrip = 10,
                    PurchasePrice = 18.00m,
                    SellingPriceStrip = 30.00m,
                    SellingPriceTablet = 3.00m
                },
                new Batch
                {
                    MedicineId = GetMedicineId("Ibuprofen 400mg"),
                    BatchNumber = "IBU2024002",
                    ExpiryDate = new DateTime(2024, 9, 15),
                    StripQuantity = 60,
                    TabletPerStrip = 10,
                    PurchasePrice = 17.50m,
                    SellingPriceStrip = 28.00m,
                    SellingPriceTablet = 2.80m
                },
                // Omeprazole batches
                new Batch
                {
                    MedicineId = GetMedicineId("Omeprazole 20mg"),
                    BatchNumber = "OME2024001",
                    ExpiryDate = new DateTime(2026, 3, 25),
                    StripQuantity = 90,
                    TabletPerStrip = 10,
                    PurchasePrice = 22.00m,
                    SellingPriceStrip = 38.00m,
                    SellingPriceTablet = 3.80m
                },
                // Metformin batches
                new Batch
                {
                    MedicineId = GetMedicineId("Metformin 500mg"),
                    BatchNumber = "MET2024001",
                    ExpiryDate = new DateTime(2025, 7, 10),
                    StripQuantity = 200,
                    TabletPerStrip = 10,
                    PurchasePrice = 12.00m,
                    SellingPriceStrip = 20.00m,
                    SellingPriceTablet = 2.00m
                },
                new Batch
                {
                    MedicineId = GetMedicineId("Metformin 500mg"),
                    BatchNumber = "MET2024002",
                    ExpiryDate = new DateTime(2026, 1, 15),
                    StripQuantity = 150,
                    TabletPerStrip = 10,
                    PurchasePrice = 11.50m,
                    SellingPriceStrip = 19.00m,
                    SellingPriceTablet = 1.90m
                },
                // Atorvastatin batches
                new Batch
                {
                    MedicineId = GetMedicineId("Atorvastatin 10mg"),
                    BatchNumber = "ATO2024001",
                    ExpiryDate = new DateTime(2025, 9, 5),
                    StripQuantity = 70,
                    TabletPerStrip = 10,
                    PurchasePrice = 85.00m,
                    SellingPriceStrip = 120.00m,
                    SellingPriceTablet = 12.00m
                },
                // Levothyroxine batches
                new Batch
                {
                    MedicineId = GetMedicineId("Levothyroxine 50mcg"),
                    BatchNumber = "LEV2024001",
                    ExpiryDate = new DateTime(2026, 4, 12),
                    StripQuantity = 100,
                    TabletPerStrip = 10,
                    PurchasePrice = 25.00m,
                    SellingPriceStrip = 45.00m,
                    SellingPriceTablet = 4.50m
                },
                // Cetirizine batches
                new Batch
                {
                    MedicineId = GetMedicineId("Cetirizine 10mg"),
                    BatchNumber = "CET2024001",
                    ExpiryDate = new DateTime(2025, 12, 20),
                    StripQuantity = 130,
                    TabletPerStrip = 10,
                    PurchasePrice = 16.00m,
                    SellingPriceStrip = 28.00m,
                    SellingPriceTablet = 2.80m
                },
                // Vitamin D3 batches
                new Batch
                {
                    MedicineId = GetMedicineId("Vitamin D3 1000 IU"),
                    BatchNumber = "VIT2024001",
                    ExpiryDate = new DateTime(2027, 2, 28),
                    StripQuantity = 60,
                    TabletPerStrip = 10,
                    PurchasePrice = 30.00m,
                    SellingPriceStrip = 50.00m,
                    SellingPriceTablet = 5.00m
                }
            };

            // Seed batches only if they are missing (by batch number)
            var existingBatchNumbers = context.Batches.AsNoTracking().Select(b => b.BatchNumber).ToHashSet();
            var newBatches = batches.Where(b => !existingBatchNumbers.Contains(b.BatchNumber)).ToList();
            if (newBatches.Any())
            {
                context.Batches.AddRange(newBatches);
                context.SaveChanges();
            }

            Console.WriteLine("Demo data seeded successfully!");
            Console.WriteLine($"- {customers.Count} customers added");
            Console.WriteLine($"- {medicines.Count} medicines added");
            Console.WriteLine($"- {batches.Count} batches added");
        }
    }
}
