using Microsoft.EntityFrameworkCore;
using Rxnxt.Business.Data;
using Rxnxt.Business.Implementations;
using Rxnxt.Business.Interfaces;
using Rxnxt.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Register Entity Framework with SQL Server
builder.Services.AddDbContext<PharmacyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IMedicineRepository, MedicineRepository>();
builder.Services.AddScoped<IBatchRepository, BatchRepository>();
builder.Services.AddScoped<ISaleRepository, SaleRepository>();

// Register Logging
builder.Services.AddRxnxtLogging();

var app = builder.Build();

// Apply migrations and seed data on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();

    // Check if we should reset and reseed data (for demo purposes)
    var resetData = Environment.GetEnvironmentVariable("RESET_DEMO_DATA")?.ToLower() == "true";

    if (resetData)
    {
        db.Database.EnsureDeleted();
    }

    db.Database.EnsureCreated();

    // Seed demo data with optional force reset
    DemoDataSeeder.SeedData(db, resetData);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Sales}/{action=Index}/{id?}");

app.Run();
