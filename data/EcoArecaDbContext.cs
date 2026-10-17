using Microsoft.EntityFrameworkCore;
using EcoAreca.Models;

namespace EcoAreca.Data;

public class EcoArecaDbContext : DbContext
{
    public EcoArecaDbContext(DbContextOptions<EcoArecaDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<User> Users => Set<User>();
    public DbSet<B2BQuote> B2BQuotes => Set<B2BQuote>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<ProductInventory> ProductInventories => Set<ProductInventory>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =========================================================================
        // ORDER CONFIGURATION
        // =========================================================================
        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("Orders");
            b.HasKey(o => o.Id);
            b.Property(o => o.Id).HasMaxLength(36);
            b.Property(o => o.OrderCode).HasMaxLength(32).IsRequired();
            b.HasIndex(o => o.OrderCode).IsUnique();

            b.Property(o => o.CustomerName).HasMaxLength(128).IsRequired();
            b.Property(o => o.PhoneNumber).HasMaxLength(20).IsRequired();
            b.HasIndex(o => o.PhoneNumber);

            b.Property(o => o.Email).HasMaxLength(128);
            b.Property(o => o.AddressDetail).HasMaxLength(256).IsRequired();
            b.Property(o => o.Ward).HasMaxLength(64);
            b.Property(o => o.District).HasMaxLength(64);
            b.Property(o => o.Province).HasMaxLength(64);
            b.Property(o => o.CustomerNote).HasMaxLength(512);
            b.Property(o => o.CouponCode).HasMaxLength(32);

            b.Property(o => o.Subtotal).HasColumnType("decimal(18,2)").HasPrecision(18, 2);
            b.Property(o => o.DiscountAmount).HasColumnType("decimal(18,2)").HasPrecision(18, 2);
            b.Property(o => o.ShippingFee).HasColumnType("decimal(18,2)").HasPrecision(18, 2);
            b.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)").HasPrecision(18, 2);

            b.Property(o => o.OrderStatus).HasConversion<string>().HasMaxLength(32);
            b.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(32);
            b.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(32);
            b.Property(o => o.ShippingMethod).HasConversion<int>();

            b.HasIndex(o => o.OrderStatus);
            b.HasIndex(o => o.PaymentStatus);
            b.HasIndex(o => o.CreatedAt);

            b.Ignore(o => o.FullShippingAddress);

            b.HasMany(o => o.Items)
             .WithOne()
             .HasForeignKey(i => i.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // =========================================================================
        // ORDER ITEM CONFIGURATION
        // =========================================================================
        modelBuilder.Entity<OrderItem>(b =>
        {
            b.ToTable("OrderItems");
            b.HasKey(i => i.Id);
            b.Property(i => i.Id).HasMaxLength(36);
            b.Property(i => i.OrderId).HasMaxLength(36).IsRequired();
            b.Property(i => i.ProductId).HasMaxLength(64).IsRequired();
            b.Property(i => i.ProductNameSnapshot).HasMaxLength(256).IsRequired();
            b.Property(i => i.VariantSnapshot).HasMaxLength(128).IsRequired();
            b.Property(i => i.ImageSnapshot).HasMaxLength(512);
            b.Property(i => i.UnitPriceSnapshot).HasColumnType("decimal(18,2)").HasPrecision(18, 2);

            b.HasIndex(i => i.OrderId);
            b.HasIndex(i => i.ProductId);

            b.Ignore(i => i.LineTotal);
        });

        // =========================================================================
        // PAYMENT CONFIGURATION
        // =========================================================================
        modelBuilder.Entity<Payment>(b =>
        {
            b.ToTable("Payments");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).HasMaxLength(36);
            b.Property(p => p.OrderId).HasMaxLength(36).IsRequired();
            b.Property(p => p.OrderCode).HasMaxLength(32).IsRequired();
            b.Property(p => p.Provider).HasMaxLength(64).IsRequired();
            b.Property(p => p.PaymentMethod).HasMaxLength(64).IsRequired();
            b.Property(p => p.TransactionReference).HasMaxLength(128);
            b.Property(p => p.Amount).HasColumnType("decimal(18,2)").HasPrecision(18, 2);
            b.Property(p => p.Currency).HasMaxLength(8);
            b.Property(p => p.PaymentStatus).HasConversion<string>().HasMaxLength(32);
            b.Property(p => p.BankCode).HasMaxLength(32);
            b.Property(p => p.PayDate).HasMaxLength(64);
            b.Property(p => p.Note).HasMaxLength(512);

            b.HasIndex(p => p.OrderId);
            b.HasIndex(p => p.OrderCode);
            b.HasIndex(p => p.TransactionReference);
        });

        // =========================================================================
        // ORDER STATUS HISTORY CONFIGURATION
        // =========================================================================
        modelBuilder.Entity<OrderStatusHistory>(b =>
        {
            b.ToTable("OrderStatusHistories");
            b.HasKey(h => h.Id);
            b.Property(h => h.Id).HasMaxLength(36);
            b.Property(h => h.OrderId).HasMaxLength(36).IsRequired();
            b.Property(h => h.OrderCode).HasMaxLength(32).IsRequired();
            b.Property(h => h.PreviousStatus).HasConversion<string>().HasMaxLength(32);
            b.Property(h => h.NewStatus).HasConversion<string>().HasMaxLength(32);
            b.Property(h => h.ChangedBy).HasMaxLength(64);
            b.Property(h => h.Note).HasMaxLength(512);

            b.HasIndex(h => h.OrderId);
            b.HasIndex(h => h.OrderCode);
            b.HasIndex(h => h.ChangedAt);
        });

        // =========================================================================
        // PAYMENT EVENT CONFIGURATION
        // =========================================================================
        modelBuilder.Entity<PaymentEvent>(b =>
        {
            b.ToTable("PaymentEvents");
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).HasMaxLength(36);
            b.Property(e => e.OrderId).HasMaxLength(36);
            b.Property(e => e.OrderCode).HasMaxLength(32);
            b.Property(e => e.Provider).HasMaxLength(64);
            b.Property(e => e.EventName).HasMaxLength(64);
            b.Property(e => e.RawPayload).HasColumnType("nvarchar(max)");
            b.Property(e => e.Note).HasMaxLength(512);

            b.HasIndex(e => e.OrderCode);
            b.HasIndex(e => e.CreatedAt);
        });

        // =========================================================================
        // USER & RBAC CONFIGURATION
        // =========================================================================
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            b.HasKey(u => u.Id);
            b.Property(u => u.Id).HasMaxLength(36);
            b.Property(u => u.Username).HasMaxLength(64).IsRequired();
            b.HasIndex(u => u.Username).IsUnique();

            b.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            b.Property(u => u.PasswordSalt).HasMaxLength(128).IsRequired();
            b.Property(u => u.FullName).HasMaxLength(128).IsRequired();
            b.Property(u => u.Email).HasMaxLength(128);
            b.Property(u => u.Role).HasMaxLength(32).IsRequired();
        });

        // =========================================================================
        // B2B QUOTE CONFIGURATION
        // =========================================================================
        modelBuilder.Entity<B2BQuote>(b =>
        {
            b.ToTable("B2BQuotes");
            b.HasKey(q => q.Id);
            b.Property(q => q.Id).HasMaxLength(36);
            b.Property(q => q.FullName).HasMaxLength(128).IsRequired();
            b.Property(q => q.Phone).HasMaxLength(20).IsRequired();
            b.Property(q => q.BusinessModel).HasMaxLength(128);
            b.Property(q => q.Demand).HasMaxLength(64);
            b.Property(q => q.Address).HasMaxLength(256);
            b.Property(q => q.QuantityExpected).HasMaxLength(64);
            b.Property(q => q.Notes).HasMaxLength(512);

            b.HasIndex(q => q.Phone);
            b.HasIndex(q => q.ReceivedAt);
        });

        // =========================================================================
        // WAREHOUSE & INVENTORY FOUNDATION CONFIGURATION
        // =========================================================================
        modelBuilder.Entity<Warehouse>(b =>
        {
            b.ToTable("Warehouses");
            b.HasKey(w => w.Id);
            b.Property(w => w.Id).HasMaxLength(36);
            b.Property(w => w.WarehouseCode).HasMaxLength(32).IsRequired();
            b.HasIndex(w => w.WarehouseCode).IsUnique();
            b.Property(w => w.WarehouseName).HasMaxLength(128).IsRequired();
            b.Property(w => w.Address).HasMaxLength(256);
        });

        modelBuilder.Entity<ProductInventory>(b =>
        {
            b.ToTable("ProductInventories");
            b.HasKey(i => i.Id);
            b.Property(i => i.Id).HasMaxLength(36);
            b.Property(i => i.WarehouseId).HasMaxLength(36).IsRequired();
            b.Property(i => i.ProductId).HasMaxLength(64).IsRequired();
            b.Property(i => i.VariantLabel).HasMaxLength(128).IsRequired();

            b.HasIndex(i => new { i.WarehouseId, i.ProductId, i.VariantLabel }).IsUnique();

            b.Ignore(i => i.AvailableQuantity);
        });

        modelBuilder.Entity<StockMovement>(b =>
        {
            b.ToTable("StockMovements");
            b.HasKey(m => m.Id);
            b.Property(m => m.Id).HasMaxLength(36);
            b.Property(m => m.WarehouseId).HasMaxLength(36).IsRequired();
            b.Property(m => m.ProductId).HasMaxLength(64).IsRequired();
            b.Property(m => m.VariantLabel).HasMaxLength(128).IsRequired();
            b.Property(m => m.MovementType).HasMaxLength(32).IsRequired();
            b.Property(m => m.ReferenceCode).HasMaxLength(64);
            b.Property(m => m.Note).HasMaxLength(512);
            b.Property(m => m.CreatedBy).HasMaxLength(64);

            b.HasIndex(m => m.ProductId);
            b.HasIndex(m => m.CreatedAt);
        });
    }
}
