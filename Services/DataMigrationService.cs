using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EcoAreca.Data;
using EcoAreca.Models;

namespace EcoAreca.Services;

public class DataMigrationReport
{
    public bool AlreadyMigrated { get; set; }
    public int JsonOrdersCount { get; set; }
    public int SqlOrdersCount { get; set; }
    public decimal JsonTotalAmountSum { get; set; }
    public decimal SqlTotalAmountSum { get; set; }
    public bool OrdersCountMatch => JsonOrdersCount == SqlOrdersCount;
    public bool TotalAmountMatch => Math.Abs(JsonTotalAmountSum - SqlTotalAmountSum) < 0.01m;
    public int PaymentsImported { get; set; }
    public int HistoryImported { get; set; }
    public int EventsImported { get; set; }
    public int QuotesImported { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DataMigrationService
{
    private readonly EcoArecaDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly AuthService _authService;

    public DataMigrationService(EcoArecaDbContext db, IWebHostEnvironment env, AuthService authService)
    {
        _db = db;
        _env = env;
        _authService = authService;
    }

    public async Task<DataMigrationReport> MigrateFromJsonToSqlServerAsync()
    {
        var report = new DataMigrationReport();

        // 1. Ensure SQL Schema is created
        await _db.Database.EnsureCreatedAsync();

        // 2. Seed Default Admin User
        await _authService.EnsureAdminSeededAsync("admin", "Admin@EcoAreca2026");

        // 3. Seed Default Warehouse
        if (!await _db.Warehouses.AnyAsync())
        {
            _db.Warehouses.Add(new Warehouse
            {
                WarehouseCode = "WH-HUE",
                WarehouseName = "Xưởng ép & Kho tổng ECO-ARECA Huế",
                Address = "Xóm 2, Thanh Vinh, Dương Nỗ, TP Huế",
                IsActive = true
            });
            await _db.SaveChangesAsync();
        }

        // Check if valid orders already exist in SQL Server
        var existingSqlOrdersCount = await _db.Orders.CountAsync();
        if (existingSqlOrdersCount > 0)
        {
            report.AlreadyMigrated = true;
            report.SqlOrdersCount = existingSqlOrdersCount;
            report.SqlTotalAmountSum = await _db.Orders.SumAsync(o => o.TotalAmount);
            report.Message = $"Dữ liệu đã tồn tại trong SQL Server ({existingSqlOrdersCount} đơn hàng). Không cần nhập lại.";
            return report;
        }

        var dataDir = Path.Combine(_env.ContentRootPath, "data");
        if (!Directory.Exists(dataDir))
        {
            report.Message = "Thư mục data/ không tồn tại. Bỏ qua nhập dữ liệu JSON.";
            return report;
        }

        var ordersFile = Path.Combine(dataDir, "orders.json");
        var paymentsFile = Path.Combine(dataDir, "payments.json");
        var historyFile = Path.Combine(dataDir, "order_history.json");
        var eventsFile = Path.Combine(dataDir, "payment_events.json");
        var quotesFile = Path.Combine(dataDir, "quotes.json");

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var importedOrderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 4. Import Orders & Items
        if (File.Exists(ordersFile))
        {
            var content = await File.ReadAllTextAsync(ordersFile);
            var jsonOrders = JsonSerializer.Deserialize<List<Order>>(content, jsonOptions) ?? new();

            // Filter out empty ghost records (where OrderCode is empty and total is 0)
            var validOrders = jsonOrders.Where(o => !string.IsNullOrWhiteSpace(o.OrderCode)).ToList();

            report.JsonOrdersCount = validOrders.Count;
            report.JsonTotalAmountSum = validOrders.Sum(o => o.TotalAmount);

            foreach (var o in validOrders)
            {
                o.CustomerName = string.IsNullOrWhiteSpace(o.CustomerName) ? "Khách Hàng" : o.CustomerName;
                o.PhoneNumber = string.IsNullOrWhiteSpace(o.PhoneNumber) ? "0900000000" : o.PhoneNumber;
                o.AddressDetail = string.IsNullOrWhiteSpace(o.AddressDetail) ? "Địa chỉ nhận hàng" : o.AddressDetail;
                o.Ward ??= string.Empty;
                o.District ??= string.Empty;
                o.Province ??= string.Empty;
                o.Email ??= string.Empty;
                o.CustomerNote ??= string.Empty;
                o.CouponCode ??= string.Empty;

                if (o.Items != null)
                {
                    foreach (var it in o.Items)
                    {
                        it.OrderId = o.Id;
                        it.ProductNameSnapshot ??= "Sản phẩm mo cau";
                        it.VariantSnapshot ??= "Tiêu chuẩn";
                        it.ImageSnapshot ??= string.Empty;
                    }
                }

                _db.Orders.Add(o);
                importedOrderIds.Add(o.Id);
            }
            await _db.SaveChangesAsync();
        }

        // 5. Import Payments
        if (File.Exists(paymentsFile))
        {
            var content = await File.ReadAllTextAsync(paymentsFile);
            var jsonPayments = JsonSerializer.Deserialize<List<Payment>>(content, jsonOptions) ?? new();
            int pCount = 0;
            foreach (var p in jsonPayments)
            {
                p.OrderId ??= string.Empty;
                p.OrderCode ??= string.Empty;
                p.Provider = string.IsNullOrWhiteSpace(p.Provider) ? "COD" : p.Provider;
                p.PaymentMethod = string.IsNullOrWhiteSpace(p.PaymentMethod) ? "COD" : p.PaymentMethod;
                p.TransactionReference ??= string.Empty;
                p.Currency = string.IsNullOrWhiteSpace(p.Currency) ? "VND" : p.Currency;
                p.BankCode ??= string.Empty;
                p.PayDate ??= string.Empty;
                p.Note ??= string.Empty;

                _db.Payments.Add(p);
                pCount++;
            }
            await _db.SaveChangesAsync();
            report.PaymentsImported = pCount;
        }

        // 6. Import History
        if (File.Exists(historyFile))
        {
            var content = await File.ReadAllTextAsync(historyFile);
            var jsonHistory = JsonSerializer.Deserialize<List<OrderStatusHistory>>(content, jsonOptions) ?? new();
            int hCount = 0;
            foreach (var h in jsonHistory)
            {
                h.OrderId ??= string.Empty;
                h.OrderCode ??= string.Empty;
                h.ChangedBy = string.IsNullOrWhiteSpace(h.ChangedBy) ? "System" : h.ChangedBy;
                h.Note ??= string.Empty;

                _db.OrderStatusHistories.Add(h);
                hCount++;
            }
            await _db.SaveChangesAsync();
            report.HistoryImported = hCount;
        }

        // 7. Import Events
        if (File.Exists(eventsFile))
        {
            var content = await File.ReadAllTextAsync(eventsFile);
            var jsonEvents = JsonSerializer.Deserialize<List<PaymentEvent>>(content, jsonOptions) ?? new();
            int eCount = 0;
            foreach (var e in jsonEvents)
            {
                e.OrderId ??= string.Empty;
                e.OrderCode ??= string.Empty;
                e.Provider = string.IsNullOrWhiteSpace(e.Provider) ? "Unknown" : e.Provider;
                e.EventName = string.IsNullOrWhiteSpace(e.EventName) ? "GenericEvent" : e.EventName;
                e.RawPayload ??= string.Empty;
                e.Note ??= string.Empty;

                _db.PaymentEvents.Add(e);
                eCount++;
            }
            await _db.SaveChangesAsync();
            report.EventsImported = eCount;
        }

        // 8. Import Quotes
        if (File.Exists(quotesFile))
        {
            try
            {
                var content = await File.ReadAllTextAsync(quotesFile);
                using var doc = JsonDocument.Parse(content);
                int qCount = 0;
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var id = el.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N")[..8];
                    DateTime receivedAt = DateTime.UtcNow;
                    if (el.TryGetProperty("receivedAt", out var rProp) && DateTime.TryParse(rProp.GetString(), out var dt))
                    {
                        receivedAt = dt;
                    }
                    var dataEl = el.GetProperty("data");
                    var quote = new B2BQuote
                    {
                        Id = id,
                        FullName = dataEl.TryGetProperty("fullName", out var fn) ? fn.GetString() ?? "" : "",
                        Phone = dataEl.TryGetProperty("phone", out var ph) ? ph.GetString() ?? "" : "",
                        BusinessModel = dataEl.TryGetProperty("businessModel", out var bm) ? bm.GetString() ?? "" : "",
                        Demand = dataEl.TryGetProperty("demand", out var dm) ? dm.GetString() ?? "" : "",
                        Address = dataEl.TryGetProperty("address", out var ad) ? ad.GetString() ?? "" : "",
                        QuantityExpected = dataEl.TryGetProperty("quantityExpected", out var qe) ? qe.GetString() ?? "" : "",
                        Notes = dataEl.TryGetProperty("notes", out var nt) ? nt.GetString() ?? "" : "",
                        ReceivedAt = receivedAt
                    };
                    _db.B2BQuotes.Add(quote);
                    qCount++;
                }
                await _db.SaveChangesAsync();
                report.QuotesImported = qCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DataMigration] Warning parsing quotes.json: {ex.Message}");
            }
        }

        // 9. Reconcile SQL Server
        report.SqlOrdersCount = await _db.Orders.CountAsync();
        report.SqlTotalAmountSum = await _db.Orders.SumAsync(o => o.TotalAmount);
        report.Message = "Nhập dữ liệu từ JSON sang SQL Server thành công.";

        return report;
    }
}
