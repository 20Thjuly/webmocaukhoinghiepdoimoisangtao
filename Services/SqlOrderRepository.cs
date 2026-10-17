using Microsoft.EntityFrameworkCore;
using EcoAreca.Data;
using EcoAreca.Models;

namespace EcoAreca.Services;

public class SqlOrderRepository : IOrderRepository
{
    private readonly EcoArecaDbContext _db;
    private static readonly SemaphoreSlim _codeGenLock = new(1, 1);

    public SqlOrderRepository(EcoArecaDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateUniqueOrderCodeAsync()
    {
        await _codeGenLock.WaitAsync();
        try
        {
            var datePrefix = DateTime.UtcNow.ToString("yyMMdd");
            var rnd = new Random();

            for (int i = 0; i < 50; i++)
            {
                var candidate = $"ECO-{datePrefix}-{rnd.Next(1000, 9999)}";
                var exists = await _db.Orders.AnyAsync(o => o.OrderCode == candidate);
                if (!exists) return candidate;
            }

            return $"ECO-{datePrefix}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
        }
        finally
        {
            _codeGenLock.Release();
        }
    }

    public async Task<Order> CreateOrderAsync(Order order, Payment payment)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
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

            // Ensure foreign key relation for items
            foreach (var item in order.Items)
            {
                item.OrderId = order.Id;
            }

            _db.Orders.Add(order);
            _db.Payments.Add(payment);
            _db.OrderStatusHistories.Add(historyEntry);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return order;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Order?> GetOrderByIdAsync(string id)
    {
        return await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order?> GetOrderByCodeAsync(string orderCode)
    {
        return await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderCode.ToLower() == orderCode.Trim().ToLower());
    }

    public async Task<Order?> LookupOrderAsync(string orderCode, string phoneNumber)
    {
        var cleanCode = orderCode.Trim().ToLower();
        var cleanPhone = phoneNumber.Trim().Replace(" ", "").Replace(".", "").Replace("-", "");
        var last9 = cleanPhone.Length >= 9 ? cleanPhone[^9..] : cleanPhone;

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.OrderCode.ToLower() == cleanCode)
            .ToListAsync();

        return orders.FirstOrDefault(o =>
        {
            var p = o.PhoneNumber.Replace(" ", "").Replace(".", "").Replace("-", "");
            return p == cleanPhone || p.EndsWith(last9);
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
        var query = _db.Orders.Include(o => o.Items).AsQueryable();

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
                o.OrderCode.Contains(s) ||
                o.CustomerName.Contains(s) ||
                o.PhoneNumber.Contains(s)
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

        return await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    public async Task<List<Payment>> GetPaymentsByOrderIdAsync(string orderId)
    {
        return await _db.Payments
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<OrderStatusHistory>> GetHistoryByOrderIdAsync(string orderId)
    {
        return await _db.OrderStatusHistories
            .Where(h => h.OrderId == orderId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
    }

    public async Task<bool> UpdateOrderStatusAsync(string orderId, OrderStatus newStatus, string changedBy, string? note)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return false;

        var oldStatus = order.OrderStatus;
        if (oldStatus == newStatus) return true;

        // Enforce State Machine validation
        if (!OrderStateMachine.CanTransition(oldStatus, newStatus, out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        order.OrderStatus = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        var historyEntry = new OrderStatusHistory
        {
            OrderId = order.Id,
            OrderCode = order.OrderCode,
            PreviousStatus = oldStatus,
            NewStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = changedBy,
            Note = note ?? $"Cập nhật trạng thái sang {newStatus}"
        };

        _db.OrderStatusHistories.Add(historyEntry);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ConfirmPaymentAsync(string orderId, string transactionRef, decimal amount, string changedBy, string? note)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return false;

            order.PaymentStatus = PaymentStatus.Paid;
            if (order.OrderStatus == OrderStatus.PendingConfirmation)
            {
                order.OrderStatus = OrderStatus.Confirmed;
            }
            order.UpdatedAt = DateTime.UtcNow;

            var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
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
                _db.Payments.Add(payment);
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

            var historyEntry = new OrderStatusHistory
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PreviousStatus = order.OrderStatus,
                NewStatus = order.OrderStatus,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = changedBy,
                Note = $"Đã xác nhận thanh toán {amount:N0}đ (Mã GD: {transactionRef}). {note}".Trim()
            };
            _db.OrderStatusHistories.Add(historyEntry);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdatePaymentStatusByCodeAsync(string orderCode, PaymentStatus status, string transactionRef, string bankCode, string? note)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderCode.ToLower() == orderCode.Trim().ToLower());
            if (order == null) return false;

            order.PaymentStatus = status;
            if (status == PaymentStatus.Paid && order.OrderStatus == OrderStatus.PendingConfirmation)
            {
                order.OrderStatus = OrderStatus.Confirmed;
            }
            order.UpdatedAt = DateTime.UtcNow;

            var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderCode.ToLower() == orderCode.Trim().ToLower());
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
                _db.Payments.Add(payment);
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

            var historyEntry = new OrderStatusHistory
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PreviousStatus = order.OrderStatus,
                NewStatus = order.OrderStatus,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = "PaymentGateway",
                Note = $"Cập nhật trạng thái thanh toán sang {status}. {note}".Trim()
            };
            _db.OrderStatusHistories.Add(historyEntry);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task RecordPaymentEventAsync(PaymentEvent evt)
    {
        _db.PaymentEvents.Add(evt);
        await _db.SaveChangesAsync();
    }
}
