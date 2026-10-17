using System.Text.Json;
using EcoAreca.Models;

namespace EcoAreca.Services;

public class OrderRepository
{
    private readonly string _dataDir;
    private readonly string _ordersFile;
    private readonly string _paymentsFile;
    private readonly string _historyFile;
    private readonly string _eventsFile;
    private readonly string _quotesFile;

    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public OrderRepository(IWebHostEnvironment env)
    {
        _dataDir = Path.Combine(env.ContentRootPath, "data");
        if (!Directory.Exists(_dataDir)) Directory.CreateDirectory(_dataDir);

        _ordersFile = Path.Combine(_dataDir, "orders.json");
        _paymentsFile = Path.Combine(_dataDir, "payments.json");
        _historyFile = Path.Combine(_dataDir, "order_history.json");
        _eventsFile = Path.Combine(_dataDir, "payment_events.json");
        _quotesFile = Path.Combine(_dataDir, "quotes.json");
    }

    private async Task<List<T>> ReadListAsync<T>(string filePath)
    {
        if (!File.Exists(filePath)) return new List<T>();
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(content)) return new List<T>();
            return JsonSerializer.Deserialize<List<T>>(content, _jsonOptions) ?? new List<T>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OrderRepository] Error reading {filePath}: {ex.Message}");
            return new List<T>();
        }
    }

    private async Task WriteListAsync<T>(string filePath, List<T> list)
    {
        var tempFile = filePath + ".tmp";
        var json = JsonSerializer.Serialize(list, _jsonOptions);
        await File.WriteAllTextAsync(tempFile, json);
        File.Move(tempFile, filePath, overwrite: true);
    }

    public async Task<string> GenerateUniqueOrderCodeAsync()
    {
        var orders = await ReadListAsync<Order>(_ordersFile);
        var existingCodes = new HashSet<string>(orders.Select(o => o.OrderCode), StringComparer.OrdinalIgnoreCase);

        var datePrefix = DateTime.UtcNow.ToString("yyMMdd");
        var rnd = new Random();

        for (int i = 0; i < 50; i++)
        {
            var code = $"ECO-{datePrefix}-{rnd.Next(1000, 9999)}";
            if (!existingCodes.Contains(code)) return code;
        }

        return $"ECO-{datePrefix}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
    }

    public async Task<Order> CreateOrderAsync(Order order, Payment payment)
    {
        await _lock.WaitAsync();
        try
        {
            var orders = await ReadListAsync<Order>(_ordersFile);
            var payments = await ReadListAsync<Payment>(_paymentsFile);
            var history = await ReadListAsync<OrderStatusHistory>(_historyFile);

            if (string.IsNullOrWhiteSpace(order.OrderCode))
            {
                order.OrderCode = await GenerateUniqueOrderCodeAsync();
            }

            order.CreatedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            payment.OrderId = order.Id;
            payment.OrderCode = order.OrderCode;
            payment.Amount = order.TotalAmount;
            payment.CreatedAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;

            var historyEntry = new OrderStatusHistory
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PreviousStatus = OrderStatus.PendingConfirmation,
                NewStatus = order.OrderStatus,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = "Khách hàng",
                Note = "Tạo đơn hàng mới qua website"
            };

            orders.Insert(0, order);
            payments.Insert(0, payment);
            history.Insert(0, historyEntry);

            await WriteListAsync(_ordersFile, orders);
            await WriteListAsync(_paymentsFile, payments);
            await WriteListAsync(_historyFile, history);

            return order;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Order?> GetOrderByIdAsync(string id)
    {
        var orders = await ReadListAsync<Order>(_ordersFile);
        return orders.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Order?> GetOrderByCodeAsync(string orderCode)
    {
        var orders = await ReadListAsync<Order>(_ordersFile);
        return orders.FirstOrDefault(o => o.OrderCode.Equals(orderCode, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Order?> LookupOrderAsync(string orderCode, string phoneNumber)
    {
        var cleanCode = orderCode.Trim();
        var cleanPhone = phoneNumber.Trim().Replace(" ", "").Replace(".", "").Replace("-", "");
        var last9 = cleanPhone.Length >= 9 ? cleanPhone[^9..] : cleanPhone;

        var orders = await ReadListAsync<Order>(_ordersFile);
        return orders.FirstOrDefault(o =>
        {
            if (!o.OrderCode.Equals(cleanCode, StringComparison.OrdinalIgnoreCase)) return false;
            var orderPhone = o.PhoneNumber.Replace(" ", "").Replace(".", "").Replace("-", "");
            return orderPhone == cleanPhone || orderPhone.EndsWith(last9);
        });
    }

    public async Task<List<Order>> GetAllOrdersAsync(
        OrderStatus? status = null, 
        PaymentStatus? paymentStatus = null, 
        PaymentMethod? paymentMethod = null, 
        string? search = null, 
        DateTime? fromDate = null, 
        DateTime? toDate = null)
    {
        var orders = await ReadListAsync<Order>(_ordersFile);
        var query = orders.AsEnumerable();

        if (status.HasValue)
        {
            query = query.Where(o => o.OrderStatus == status.Value);
        }

        if (paymentStatus.HasValue)
        {
            query = query.Where(o => o.PaymentStatus == paymentStatus.Value);
        }

        if (paymentMethod.HasValue)
        {
            query = query.Where(o => o.PaymentMethod == paymentMethod.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(o => 
                o.OrderCode.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                o.CustomerName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                o.PhoneNumber.Contains(s, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= toDate.Value);
        }

        return query.OrderByDescending(o => o.CreatedAt).ToList();
    }

    public async Task<List<Payment>> GetPaymentsByOrderIdAsync(string orderId)
    {
        var payments = await ReadListAsync<Payment>(_paymentsFile);
        return payments.Where(p => p.OrderId.Equals(orderId, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<List<OrderStatusHistory>> GetHistoryByOrderIdAsync(string orderId)
    {
        var history = await ReadListAsync<OrderStatusHistory>(_historyFile);
        return history.Where(h => h.OrderId.Equals(orderId, StringComparison.OrdinalIgnoreCase))
                      .OrderByDescending(h => h.ChangedAt)
                      .ToList();
    }

    public async Task<bool> UpdateOrderStatusAsync(string orderId, OrderStatus newStatus, string changedBy, string? note)
    {
        await _lock.WaitAsync();
        try
        {
            var orders = await ReadListAsync<Order>(_ordersFile);
            var order = orders.FirstOrDefault(o => o.Id.Equals(orderId, StringComparison.OrdinalIgnoreCase));
            if (order == null) return false;

            var oldStatus = order.OrderStatus;
            if (oldStatus == newStatus) return true;

            order.OrderStatus = newStatus;
            order.UpdatedAt = DateTime.UtcNow;

            var history = await ReadListAsync<OrderStatusHistory>(_historyFile);
            history.Insert(0, new OrderStatusHistory
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PreviousStatus = oldStatus,
                NewStatus = newStatus,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = changedBy,
                Note = note ?? $"Cập nhật trạng thái sang {newStatus}"
            });

            await WriteListAsync(_ordersFile, orders);
            await WriteListAsync(_historyFile, history);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ConfirmPaymentAsync(string orderId, string transactionRef, decimal amount, string changedBy, string? note)
    {
        await _lock.WaitAsync();
        try
        {
            var orders = await ReadListAsync<Order>(_ordersFile);
            var order = orders.FirstOrDefault(o => o.Id.Equals(orderId, StringComparison.OrdinalIgnoreCase));
            if (order == null) return false;

            order.PaymentStatus = PaymentStatus.Paid;
            if (order.OrderStatus == OrderStatus.PendingConfirmation)
            {
                order.OrderStatus = OrderStatus.Confirmed;
            }
            order.UpdatedAt = DateTime.UtcNow;

            var payments = await ReadListAsync<Payment>(_paymentsFile);
            var payment = payments.FirstOrDefault(p => p.OrderId.Equals(orderId, StringComparison.OrdinalIgnoreCase));
            if (payment == null)
            {
                payment = new Payment
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    PaymentMethod = order.PaymentMethod.ToString(),
                    Provider = "ManualVerification",
                    Amount = amount,
                    PaymentStatus = PaymentStatus.Paid,
                    TransactionReference = transactionRef,
                    Note = note ?? "Xác nhận thanh toán thủ công bởi quản trị viên",
                    PayDate = DateTime.UtcNow.ToString("o")
                };
                payments.Insert(0, payment);
            }
            else
            {
                payment.PaymentStatus = PaymentStatus.Paid;
                payment.TransactionReference = transactionRef;
                payment.Amount = amount > 0 ? amount : payment.Amount;
                payment.Note = note ?? "Xác nhận thanh toán thủ công bởi quản trị viên";
                payment.PayDate = DateTime.UtcNow.ToString("o");
                payment.UpdatedAt = DateTime.UtcNow;
            }

            var history = await ReadListAsync<OrderStatusHistory>(_historyFile);
            history.Insert(0, new OrderStatusHistory
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PreviousStatus = order.OrderStatus,
                NewStatus = order.OrderStatus,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = changedBy,
                Note = $"Đã xác nhận thanh toán {amount:N0}đ (Mã GD: {transactionRef}). {note}".Trim()
            });

            await WriteListAsync(_ordersFile, orders);
            await WriteListAsync(_paymentsFile, payments);
            await WriteListAsync(_historyFile, history);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> UpdatePaymentStatusByCodeAsync(string orderCode, PaymentStatus status, string transactionRef, string bankCode, string? note)
    {
        await _lock.WaitAsync();
        try
        {
            var orders = await ReadListAsync<Order>(_ordersFile);
            var order = orders.FirstOrDefault(o => o.OrderCode.Equals(orderCode, StringComparison.OrdinalIgnoreCase));
            if (order == null) return false;

            order.PaymentStatus = status;
            if (status == PaymentStatus.Paid && order.OrderStatus == OrderStatus.PendingConfirmation)
            {
                order.OrderStatus = OrderStatus.Confirmed;
            }
            order.UpdatedAt = DateTime.UtcNow;

            var payments = await ReadListAsync<Payment>(_paymentsFile);
            var payment = payments.FirstOrDefault(p => p.OrderCode.Equals(orderCode, StringComparison.OrdinalIgnoreCase));
            if (payment == null)
            {
                payment = new Payment
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    PaymentMethod = order.PaymentMethod.ToString(),
                    Provider = "OnlineGateway",
                    Amount = order.TotalAmount,
                    PaymentStatus = status,
                    TransactionReference = transactionRef,
                    BankCode = bankCode,
                    Note = note ?? string.Empty,
                    PayDate = DateTime.UtcNow.ToString("o")
                };
                payments.Insert(0, payment);
            }
            else
            {
                payment.PaymentStatus = status;
                payment.TransactionReference = transactionRef;
                payment.BankCode = bankCode;
                payment.Note = note ?? payment.Note;
                payment.PayDate = DateTime.UtcNow.ToString("o");
                payment.UpdatedAt = DateTime.UtcNow;
            }

            var history = await ReadListAsync<OrderStatusHistory>(_historyFile);
            history.Insert(0, new OrderStatusHistory
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PreviousStatus = order.OrderStatus,
                NewStatus = order.OrderStatus,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = "PaymentGateway",
                Note = $"Cập nhật trạng thái thanh toán sang {status}. {note}".Trim()
            });

            await WriteListAsync(_ordersFile, orders);
            await WriteListAsync(_paymentsFile, payments);
            await WriteListAsync(_historyFile, history);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RecordPaymentEventAsync(PaymentEvent evt)
    {
        await _lock.WaitAsync();
        try
        {
            var events = await ReadListAsync<PaymentEvent>(_eventsFile);
            events.Insert(0, evt);
            await WriteListAsync(_eventsFile, events);
        }
        finally
        {
            _lock.Release();
        }
    }
}
