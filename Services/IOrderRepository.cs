using EcoAreca.Models;

namespace EcoAreca.Services;

public interface IOrderRepository
{
    Task<string> GenerateUniqueOrderCodeAsync();
    Task<Order> CreateOrderAsync(Order order, Payment payment);
    Task<Order?> GetOrderByIdAsync(string id);
    Task<Order?> GetOrderByCodeAsync(string orderCode);
    Task<Order?> LookupOrderAsync(string orderCode, string phoneNumber);
    Task<List<Order>> GetAllOrdersAsync(
        OrderStatus? status = null, 
        PaymentStatus? paymentStatus = null, 
        PaymentMethod? paymentMethod = null, 
        string? search = null, 
        DateTime? fromDate = null, 
        DateTime? toDate = null);
    Task<List<Payment>> GetPaymentsByOrderIdAsync(string orderId);
    Task<List<OrderStatusHistory>> GetHistoryByOrderIdAsync(string orderId);
    Task<bool> UpdateOrderStatusAsync(string orderId, OrderStatus newStatus, string changedBy, string? note);
    Task<bool> ConfirmPaymentAsync(string orderId, string transactionRef, decimal amount, string changedBy, string? note);
    Task<bool> UpdatePaymentStatusByCodeAsync(string orderCode, PaymentStatus status, string transactionRef, string bankCode, string? note);
    Task RecordPaymentEventAsync(PaymentEvent evt);
}
