using Microsoft.EntityFrameworkCore;
using Rxnxt.Domain.Models;

namespace Rxnxt.Business.Data
{
    public class PharmacyDbContext : DbContext
    {
        public PharmacyDbContext(DbContextOptions<PharmacyDbContext> options) : base(options)
        {
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Medicine> Medicines { get; set; }
        public DbSet<Batch> Batches { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<Payment> Payments { get; set; }

        public DbSet<SaleHeaderRow> SaleHeaders { get; set; }
        public DbSet<SaleDetailRow> SaleDetails { get; set; }
        public DbSet<SalePaymentRow> SalePayments { get; set; }
        public DbSet<ProductMasterRow> ProductMasters { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("dbo");

            modelBuilder.Entity<SaleHeaderRow>().ToTable("SaleHeader");
            modelBuilder.Entity<SaleDetailRow>().ToTable("SaleDetail");
            modelBuilder.Entity<SalePaymentRow>().ToTable("SalePayment");
            modelBuilder.Entity<ProductMasterRow>().ToTable("ProductMaster");

            // Customer
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasIndex(e => e.Phone).IsUnique();
                entity.HasIndex(e => e.Name);
            });

            // Medicine
            modelBuilder.Entity<Medicine>(entity =>
            {
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.GenericName);
            });

            // Batch
            modelBuilder.Entity<Batch>(entity =>
            {
                entity.HasIndex(e => e.BatchNumber);
                entity.HasOne(e => e.Medicine)
                      .WithMany(m => m.Batches)
                      .HasForeignKey(e => e.MedicineId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Sale
            modelBuilder.Entity<Sale>(entity =>
            {
                entity.HasOne(e => e.Customer)
                      .WithMany(c => c.Sales)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => e.InvoiceNumber).IsUnique().HasFilter("[InvoiceNumber] IS NOT NULL");
            });

            // SaleItem
            modelBuilder.Entity<SaleItem>(entity =>
            {
                entity.HasOne(e => e.Sale)
                      .WithMany(s => s.SaleItems)
                      .HasForeignKey(e => e.SaleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Payment
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasOne(e => e.Sale)
                      .WithMany(s => s.Payments)
                      .HasForeignKey(e => e.SaleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed Medicines
            modelBuilder.Entity<Medicine>().HasData(
                new Medicine { Id = 1, Name = "Paracetamol 500mg", GenericName = "Acetaminophen", Manufacturer = "Cipla Ltd", Category = "Analgesic" },
                new Medicine { Id = 2, Name = "Amoxicillin 250mg", GenericName = "Amoxicillin", Manufacturer = "Sun Pharma", Category = "Antibiotic" },
                new Medicine { Id = 3, Name = "Omeprazole 20mg", GenericName = "Omeprazole", Manufacturer = "Dr. Reddy's", Category = "Antacid" },
                new Medicine { Id = 4, Name = "Cetirizine 10mg", GenericName = "Cetirizine", Manufacturer = "Cipla Ltd", Category = "Antihistamine" },
                new Medicine { Id = 5, Name = "Metformin 500mg", GenericName = "Metformin", Manufacturer = "USV Ltd", Category = "Antidiabetic" },
                new Medicine { Id = 6, Name = "Atorvastatin 10mg", GenericName = "Atorvastatin", Manufacturer = "Ranbaxy", Category = "Statin" },
                new Medicine { Id = 7, Name = "Azithromycin 500mg", GenericName = "Azithromycin", Manufacturer = "Alkem Labs", Category = "Antibiotic" },
                new Medicine { Id = 8, Name = "Pantoprazole 40mg", GenericName = "Pantoprazole", Manufacturer = "Sun Pharma", Category = "Antacid" },
                new Medicine { Id = 9, Name = "Losartan 50mg", GenericName = "Losartan", Manufacturer = "Torrent Pharma", Category = "Antihypertensive" },
                new Medicine { Id = 10, Name = "Ibuprofen 400mg", GenericName = "Ibuprofen", Manufacturer = "Abbott", Category = "NSAID" },
                new Medicine { Id = 11, Name = "Dolo 650mg", GenericName = "Paracetamol", Manufacturer = "Micro Labs", Category = "Analgesic" },
                new Medicine { Id = 12, Name = "Augmentin 625mg", GenericName = "Amoxicillin + Clavulanic Acid", Manufacturer = "GSK", Category = "Antibiotic" }
            );

            // Seed Batches
            modelBuilder.Entity<Batch>().HasData(
                new Batch { Id = 1, MedicineId = 1, BatchNumber = "PCM-2024-001", ExpiryDate = new DateTime(2026, 12, 31), StripQuantity = 500, TabletPerStrip = 10, PurchasePrice = 8.50m, SellingPriceStrip = 15.00m, SellingPriceTablet = 2.00m },
                new Batch { Id = 2, MedicineId = 1, BatchNumber = "PCM-2024-002", ExpiryDate = new DateTime(2025, 6, 30), StripQuantity = 200, TabletPerStrip = 10, PurchasePrice = 8.00m, SellingPriceStrip = 14.00m, SellingPriceTablet = 1.80m },
                new Batch { Id = 3, MedicineId = 2, BatchNumber = "AMX-2024-001", ExpiryDate = new DateTime(2026, 8, 15), StripQuantity = 300, TabletPerStrip = 10, PurchasePrice = 25.00m, SellingPriceStrip = 45.00m, SellingPriceTablet = 5.50m },
                new Batch { Id = 4, MedicineId = 3, BatchNumber = "OMP-2024-001", ExpiryDate = new DateTime(2027, 3, 20), StripQuantity = 400, TabletPerStrip = 10, PurchasePrice = 15.00m, SellingPriceStrip = 28.00m, SellingPriceTablet = 3.50m },
                new Batch { Id = 5, MedicineId = 4, BatchNumber = "CTZ-2024-001", ExpiryDate = new DateTime(2027, 1, 10), StripQuantity = 600, TabletPerStrip = 10, PurchasePrice = 5.00m, SellingPriceStrip = 12.00m, SellingPriceTablet = 1.50m },
                new Batch { Id = 6, MedicineId = 5, BatchNumber = "MET-2024-001", ExpiryDate = new DateTime(2026, 11, 25), StripQuantity = 350, TabletPerStrip = 10, PurchasePrice = 12.00m, SellingPriceStrip = 22.00m, SellingPriceTablet = 2.80m },
                new Batch { Id = 7, MedicineId = 6, BatchNumber = "ATV-2024-001", ExpiryDate = new DateTime(2027, 5, 18), StripQuantity = 250, TabletPerStrip = 10, PurchasePrice = 18.00m, SellingPriceStrip = 35.00m, SellingPriceTablet = 4.00m },
                new Batch { Id = 8, MedicineId = 7, BatchNumber = "AZM-2024-001", ExpiryDate = new DateTime(2026, 9, 30), StripQuantity = 150, TabletPerStrip = 6, PurchasePrice = 40.00m, SellingPriceStrip = 72.00m, SellingPriceTablet = 14.00m },
                new Batch { Id = 9, MedicineId = 8, BatchNumber = "PNT-2024-001", ExpiryDate = new DateTime(2027, 2, 14), StripQuantity = 450, TabletPerStrip = 10, PurchasePrice = 20.00m, SellingPriceStrip = 38.00m, SellingPriceTablet = 4.50m },
                new Batch { Id = 10, MedicineId = 9, BatchNumber = "LST-2024-001", ExpiryDate = new DateTime(2026, 7, 22), StripQuantity = 280, TabletPerStrip = 10, PurchasePrice = 22.00m, SellingPriceStrip = 42.00m, SellingPriceTablet = 5.00m },
                new Batch { Id = 11, MedicineId = 10, BatchNumber = "IBU-2024-001", ExpiryDate = new DateTime(2027, 4, 5), StripQuantity = 320, TabletPerStrip = 10, PurchasePrice = 10.00m, SellingPriceStrip = 18.00m, SellingPriceTablet = 2.20m },
                new Batch { Id = 12, MedicineId = 11, BatchNumber = "DLO-2024-001", ExpiryDate = new DateTime(2026, 10, 15), StripQuantity = 700, TabletPerStrip = 15, PurchasePrice = 12.00m, SellingPriceStrip = 22.50m, SellingPriceTablet = 1.80m },
                new Batch { Id = 13, MedicineId = 12, BatchNumber = "AUG-2024-001", ExpiryDate = new DateTime(2026, 6, 28), StripQuantity = 180, TabletPerStrip = 10, PurchasePrice = 55.00m, SellingPriceStrip = 98.00m, SellingPriceTablet = 11.00m }
            );

            // Seed Customers
            modelBuilder.Entity<Customer>().HasData(
                new Customer { Id = 1, Name = "Rahul Sharma", Phone = "9876543210", Email = "rahul@email.com", Address = "123 MG Road, Mumbai", LoyaltyPoints = 150, CreatedDate = new DateTime(2024, 1, 15) },
                new Customer { Id = 2, Name = "Priya Patel", Phone = "9876543211", Email = "priya@email.com", Address = "456 Park Street, Delhi", LoyaltyPoints = 320, CreatedDate = new DateTime(2024, 2, 20) },
                new Customer { Id = 3, Name = "Amit Kumar", Phone = "9876543212", Address = "789 Gandhi Nagar, Pune", LoyaltyPoints = 75, CreatedDate = new DateTime(2024, 3, 10) },
                new Customer { Id = 4, Name = "Sneha Gupta", Phone = "9876543213", Email = "sneha@email.com", LoyaltyPoints = 200, CreatedDate = new DateTime(2024, 4, 5) },
                new Customer { Id = 5, Name = "Vikram Singh", Phone = "9876543214", Address = "321 Lake View, Bangalore", LoyaltyPoints = 50, CreatedDate = new DateTime(2024, 5, 12) }
            );
        }
    }
}
