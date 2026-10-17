using System.Text.Json.Serialization;

namespace EcoAreca.Models;

public enum OrderStatus
{
    PendingConfirmation = 0, // Chờ xác nhận
    Confirmed = 1,           // Đã xác nhận
    Processing = 2,          // Đang chuẩn bị hàng
    Shipping = 3,            // Đang giao hàng
    Completed = 4,           // Hoàn thành
    Cancelled = 5            // Đã hủy
}

public enum PaymentStatus
{
    Pending = 0,             // Chờ thanh toán
    Processing = 1,          // Đang xử lý
    Paid = 2,                // Đã thanh toán
    Failed = 3,              // Thất bại
    Cancelled = 4,           // Đã hủy
    Expired = 5,             // Hết hạn
    Refunded = 6             // Đã hoàn tiền
}

public enum PaymentMethod
{
    COD = 0,                 // Thanh toán khi nhận hàng
    BankTransfer = 1,        // Chuyển khoản ngân hàng (VietQR)
    VNPay = 2                // Cổng thanh toán trực tuyến VNPay
}

public enum ShippingMethod
{
    StandardDelivery = 0,    // Giao hàng tận nơi toàn quốc
    ShowroomPickup = 1       // Tự nhận tại Xưởng/Showroom ECO-ARECA Huế
}

public class OrderItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string VariantSnapshot { get; set; } = string.Empty;
    public string ImageSnapshot { get; set; } = string.Empty;
    public decimal UnitPriceSnapshot { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPriceSnapshot * Quantity;
}

public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Address breakdown
    public string AddressDetail { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string FullShippingAddress => $"{AddressDetail}, {Ward}, {District}, {Province}".Trim(',', ' ');

    public string CustomerNote { get; set; } = string.Empty;
    public ShippingMethod ShippingMethod { get; set; } = ShippingMethod.StandardDelivery;

    // Financial breakdown
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string CouponCode { get; set; } = string.Empty;
    public decimal ShippingFee { get; set; }
    public decimal TotalAmount { get; set; }

    // Statuses
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrderStatus OrderStatus { get; set; } = OrderStatus.PendingConfirmation;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public List<OrderItem> Items { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Payment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string OrderId { get; set; } = string.Empty;
    public string OrderCode { get; set; } = string.Empty;
    public string Provider { get; set; } = "COD"; // COD, VietQR_BankTransfer, VNPay
    public string PaymentMethod { get; set; } = "COD";
    public string TransactionReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public string BankCode { get; set; } = string.Empty;
    public string PayDate { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderStatusHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string OrderId { get; set; } = string.Empty;
    public string OrderCode { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrderStatus PreviousStatus { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrderStatus NewStatus { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string ChangedBy { get; set; } = "System"; // Customer, System, Admin
    public string Note { get; set; } = string.Empty;
}

public class PaymentEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string OrderId { get; set; } = string.Empty;
    public string OrderCode { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Request & Response DTOs
public class CartItemRequest
{
    public string ProductId { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class ValidateCartRequest
{
    public List<CartItemRequest> Items { get; set; } = new();
    public string? CouponCode { get; set; }
    public ShippingMethod ShippingMethod { get; set; } = ShippingMethod.StandardDelivery;
}

public class ValidatedCartResponse
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<OrderItem> ValidatedItems { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? AppliedCoupon { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsFreeShipping { get; set; }
    public decimal FreeShippingThresholdRemaining { get; set; }
}

public class CreateOrderRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string AddressDetail { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string? CustomerNote { get; set; }
    public ShippingMethod ShippingMethod { get; set; } = ShippingMethod.StandardDelivery;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;
    public string? CouponCode { get; set; }
    public List<CartItemRequest> Items { get; set; } = new();
}

public class CreateOrderResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string OrderCode { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public BankTransferInfo? BankTransferInfo { get; set; }
    public string? PaymentUrl { get; set; } // VNPay Payment URL
}

public class BankTransferInfo
{
    public string BankName { get; set; } = "MB Bank (Ngân hàng TMCP Quân Đội)";
    public string BankId { get; set; } = "MB";
    public string AccountNumber { get; set; } = "0388750916";
    public string AccountName { get; set; } = "NGUYEN DINH QUANG";
    public decimal Amount { get; set; }
    public string TransferContent { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
}

public class OrderLookupRequest
{
    public string OrderCode { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class UpdateOrderStatusRequest
{
    public OrderStatus NewStatus { get; set; }
    public string? Note { get; set; }
    public string AdminName { get; set; } = "Admin";
}

public class ConfirmPaymentRequest
{
    public decimal Amount { get; set; }
    public string? TransactionReference { get; set; }
    public string? Note { get; set; }
    public string AdminName { get; set; } = "Admin";
}

// =========================================================================
// SECURITY & RBAC ENTITIES
// =========================================================================
public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin"; // Admin, Warehouse, Sales, Accountant
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public UserDto? User { get; set; }
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

// =========================================================================
// B2B QUOTATION ENTITY
// =========================================================================
public class B2BQuote
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string BusinessModel { get; set; } = string.Empty;
    public string Demand { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string QuantityExpected { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

// =========================================================================
// INVENTORY & WAREHOUSE FOUNDATION ENTITIES (For Phase 2)
// =========================================================================
public class Warehouse
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string WarehouseCode { get; set; } = "WH-HUE";
    public string WarehouseName { get; set; } = "Xưởng ép & Kho tổng ECO-ARECA Huế";
    public string Address { get; set; } = "Xóm 2, Thanh Vinh, Dương Nỗ, TP Huế";
    public bool IsActive { get; set; } = true;
}

public class ProductInventory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string WarehouseId { get; set; } = "WH-HUE";
    public string ProductId { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public int PhysicalQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity => Math.Max(0, PhysicalQuantity - ReservedQuantity);
    public int LowStockThreshold { get; set; } = 50;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class StockMovement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string WarehouseId { get; set; } = "WH-HUE";
    public string ProductId { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public string MovementType { get; set; } = "OrderSale"; // PurchaseReceive, OrderSale, Adjustment, Return
    public int QuantityChanged { get; set; }
    public int BalanceAfter { get; set; }
    public string ReferenceCode { get; set; } = string.Empty; // OrderCode or PO Code
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
}

// =========================================================================
// STATE MACHINE VALIDATOR
// =========================================================================
public static class OrderStateMachine
{
    private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> AllowedOrderTransitions = new()
    {
        [OrderStatus.PendingConfirmation] = new() { OrderStatus.Confirmed, OrderStatus.Cancelled },
        [OrderStatus.Confirmed] = new() { OrderStatus.Processing, OrderStatus.Cancelled },
        [OrderStatus.Processing] = new() { OrderStatus.Shipping, OrderStatus.Cancelled },
        [OrderStatus.Shipping] = new() { OrderStatus.Completed, OrderStatus.Cancelled },
        [OrderStatus.Completed] = new(), // Final state
        [OrderStatus.Cancelled] = new()  // Final state
    };

    public static bool CanTransition(OrderStatus current, OrderStatus target, out string reason)
    {
        if (current == target)
        {
            reason = "Trạng thái không thay đổi.";
            return true;
        }

        if (AllowedOrderTransitions.TryGetValue(current, out var targets) && targets.Contains(target))
        {
            reason = string.Empty;
            return true;
        }

        reason = $"Không được phép chuyển trạng thái từ '{current}' sang '{target}'.";
        return false;
    }
}
