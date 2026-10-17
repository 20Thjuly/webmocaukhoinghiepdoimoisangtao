using System.Text.Json;
using EcoAreca.Data;
using EcoAreca.Models;
using EcoAreca.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Configure port
builder.WebHost.UseUrls("http://localhost:5000");

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Configure SQL Server Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=EcoArecaDb;Integrated Security=True;TrustServerCertificate=True";

builder.Services.AddDbContext<EcoArecaDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// Configure Services
builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<DataMigrationService>();
builder.Services.AddSingleton<ProductCatalogService>();

var vnpayConfig = builder.Configuration.GetSection("VNPay").Get<VNPayConfig>() ?? new VNPayConfig();
builder.Services.AddSingleton(vnpayConfig);
builder.Services.AddSingleton<VNPayService>();

var app = builder.Build();

app.UseCors("AllowAll");

// Serve static files from project root
var contentRoot = app.Environment.ContentRootPath;
var fileServerOptions = new FileServerOptions
{
    FileProvider = new PhysicalFileProvider(contentRoot),
    RequestPath = "",
    EnableDefaultFiles = true
};
fileServerOptions.DefaultFilesOptions.DefaultFileNames.Clear();
fileServerOptions.DefaultFilesOptions.DefaultFileNames.Add("index.html");

app.UseFileServer(fileServerOptions);

// Startup Data Migration & Seed
using (var scope = app.Services.CreateScope())
{
    try
    {
        var migrationService = scope.ServiceProvider.GetRequiredService<DataMigrationService>();
        var report = await migrationService.MigrateFromJsonToSqlServerAsync();
        Console.WriteLine($"[Database] {report.Message} | Orders in SQL: {report.SqlOrdersCount} | Revenue: {report.SqlTotalAmountSum:N0} VND");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Database] Migration error: {ex.Message}");
    }
}

// Config references
var catalogService = app.Services.GetRequiredService<ProductCatalogService>();
var vnpayService = app.Services.GetRequiredService<VNPayService>();
var config = app.Configuration;

var adminApiKey = config.GetValue<string>("Admin:ApiKey") ?? "EcoAreca@Admin2026";
var bankName = config.GetValue<string>("BankTransfer:BankName") ?? "MB Bank (Ngân hàng TMCP Quân Đội)";
var bankId = config.GetValue<string>("BankTransfer:BankId") ?? "MB";
var bankAccountNo = config.GetValue<string>("BankTransfer:AccountNumber") ?? "0388750916";
var bankAccountName = config.GetValue<string>("BankTransfer:AccountName") ?? "NGUYEN DINH QUANG";

// Security helper: checks Bearer token or legacy X-Admin-Key header (query string removed for security)
bool IsAdminAuthorized(HttpContext ctx, AuthService authService)
{
    // 1. Check Bearer Token (Secure Session Token)
    if (ctx.Request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        var token = authHeader.ToString().Trim();
        if (authService.ValidateSessionToken(token, out _, out _))
        {
            return true;
        }
    }

    // 2. Check X-Admin-Key header (Token or legacy key fallback)
    if (ctx.Request.Headers.TryGetValue("X-Admin-Key", out var headerVal))
    {
        var keyStr = headerVal.ToString().Trim();
        if (authService.ValidateSessionToken(keyStr, out _, out _))
        {
            return true;
        }
        if (keyStr == adminApiKey)
        {
            return true;
        }
    }

    return false;
}

// =========================================================================
// 1. AUTHENTICATION & RBAC ENDPOINTS
// =========================================================================
app.MapPost("/api/auth/login", async (LoginRequest request, AuthService authService) =>
{
    var response = await authService.LoginAsync(request.Username, request.Password);
    return response.Success ? Results.Ok(response) : Results.BadRequest(response);
});

app.MapGet("/api/auth/me", (HttpContext ctx, AuthService authService) =>
{
    if (ctx.Request.Headers.TryGetValue("Authorization", out var hVal) &&
        authService.ValidateSessionToken(hVal.ToString(), out var username, out var role))
    {
        return Results.Ok(new { success = true, username, role });
    }
    if (ctx.Request.Headers.TryGetValue("X-Admin-Key", out var aVal) &&
        authService.ValidateSessionToken(aVal.ToString(), out var u, out var r))
    {
        return Results.Ok(new { success = true, username = u, role = r });
    }
    return Results.Unauthorized();
});

// =========================================================================
// 2. CART VALIDATION & PRICE CALCULATION ENDPOINT
// =========================================================================
app.MapPost("/api/order/validate-cart", (ValidateCartRequest request) =>
{
    try
    {
        var validation = catalogService.ValidateAndCalculateCart(request.Items, request.CouponCode, request.ShippingMethod);
        return Results.Ok(validation);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

// =========================================================================
// 3. CREATE ORDER ENDPOINT (Authoritative Server-Side Calculation & SQL)
// =========================================================================
app.MapPost("/api/order/create", async (CreateOrderRequest request, HttpContext httpContext, IOrderRepository orderRepo) =>
{
    try
    {
        // 1. Validate Customer Info
        if (string.IsNullOrWhiteSpace(request.CustomerName) || request.CustomerName.Trim().Length < 2)
        {
            return Results.BadRequest(new { success = false, message = "Vui lòng nhập họ và tên hợp lệ (tối thiểu 2 ký tự)." });
        }

        var cleanPhone = request.PhoneNumber.Trim().Replace(" ", "").Replace(".", "").Replace("-", "");
        if (cleanPhone.Length < 10)
        {
            return Results.BadRequest(new { success = false, message = "Vui lòng nhập số điện thoại Việt Nam hợp lệ (10 chữ số)." });
        }

        if (string.IsNullOrWhiteSpace(request.AddressDetail))
        {
            return Results.BadRequest(new { success = false, message = "Vui lòng nhập địa chỉ giao hàng chi tiết." });
        }

        // 2. Validate Items & Calculate Prices on Server
        var validation = catalogService.ValidateAndCalculateCart(request.Items, request.CouponCode, request.ShippingMethod);
        if (!validation.IsValid)
        {
            return Results.BadRequest(new { success = false, message = validation.ErrorMessage ?? "Giỏ hàng không hợp lệ." });
        }

        // 3. Build Order & Items
        var order = new Order
        {
            CustomerName = request.CustomerName.Trim(),
            PhoneNumber = cleanPhone,
            Email = request.Email?.Trim() ?? string.Empty,
            AddressDetail = request.AddressDetail.Trim(),
            Ward = request.Ward?.Trim() ?? string.Empty,
            District = request.District?.Trim() ?? string.Empty,
            Province = request.Province?.Trim() ?? string.Empty,
            CustomerNote = request.CustomerNote?.Trim() ?? string.Empty,
            ShippingMethod = request.ShippingMethod,
            PaymentMethod = request.PaymentMethod,
            Subtotal = validation.Subtotal,
            DiscountAmount = validation.DiscountAmount,
            CouponCode = validation.AppliedCoupon ?? string.Empty,
            ShippingFee = validation.ShippingFee,
            TotalAmount = validation.TotalAmount,
            OrderStatus = OrderStatus.PendingConfirmation,
            PaymentStatus = PaymentStatus.Pending,
            Items = validation.ValidatedItems
        };

        // 4. Build Payment record
        var payment = new Payment
        {
            PaymentMethod = request.PaymentMethod.ToString(),
            Amount = order.TotalAmount,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Pending
        };

        if (request.PaymentMethod == PaymentMethod.COD)
        {
            payment.Provider = "COD";
            payment.Note = "Thanh toán tiền mặt khi nhận hàng (COD)";
        }
        else if (request.PaymentMethod == PaymentMethod.BankTransfer)
        {
            payment.Provider = "VietQR_BankTransfer";
            payment.Note = $"Chuyển khoản VietQR đến {bankName} - STK: {bankAccountNo}";
        }
        else if (request.PaymentMethod == PaymentMethod.VNPay)
        {
            payment.Provider = "VNPay";
            payment.Note = "Cổng thanh toán trực tuyến VNPay Sandbox";
        }

        // 5. Persist to SQL Server Repository via ACID Transaction
        var createdOrder = await orderRepo.CreateOrderAsync(order, payment);

        // 6. Construct Response
        var response = new CreateOrderResponse
        {
            Success = true,
            Message = "Đơn hàng đã được tạo thành công.",
            OrderCode = createdOrder.OrderCode,
            OrderId = createdOrder.Id,
            TotalAmount = createdOrder.TotalAmount,
            PaymentMethod = createdOrder.PaymentMethod.ToString(),
            PaymentStatus = createdOrder.PaymentStatus.ToString()
        };

        // Bank Transfer details (VietQR)
        if (createdOrder.PaymentMethod == PaymentMethod.BankTransfer)
        {
            var transferContent = $"ECO {createdOrder.OrderCode}";
            var encodedAccountName = Uri.EscapeDataString(bankAccountName);
            var encodedContent = Uri.EscapeDataString(transferContent);
            var qrUrl = $"https://img.vietqr.io/image/{bankId}-{bankAccountNo}-compact2.png?amount={(long)createdOrder.TotalAmount}&addInfo={encodedContent}&accountName={encodedAccountName}";

            response.BankTransferInfo = new BankTransferInfo
            {
                BankName = bankName,
                BankId = bankId,
                AccountNumber = bankAccountNo,
                AccountName = bankAccountName,
                Amount = createdOrder.TotalAmount,
                TransferContent = transferContent,
                QrCodeUrl = qrUrl
            };
        }
        // VNPay Payment URL
        else if (createdOrder.PaymentMethod == PaymentMethod.VNPay)
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var vnpUrl = vnpayService.CreatePaymentUrl(
                createdOrder.OrderCode, 
                createdOrder.TotalAmount, 
                $"Thanh toan don hang {createdOrder.OrderCode}", 
                ip
            );
            response.PaymentUrl = vnpUrl;
        }

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[API /api/order/create] Error: {ex}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

// =========================================================================
// 4. ORDER LOOKUP / TRACKING ENDPOINT (Tra Cứu Đơn Hàng)
// =========================================================================
app.MapPost("/api/order/lookup", async (OrderLookupRequest request, IOrderRepository orderRepo) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.OrderCode) || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return Results.BadRequest(new { success = false, message = "Vui lòng nhập cả Mã đơn hàng và Số điện thoại đặt hàng." });
        }

        var order = await orderRepo.LookupOrderAsync(request.OrderCode, request.PhoneNumber);
        if (order == null)
        {
            return Results.NotFound(new { success = false, message = "Không tìm thấy đơn hàng phù hợp với thông tin đã cung cấp." });
        }

        var payments = await orderRepo.GetPaymentsByOrderIdAsync(order.Id);
        var history = await orderRepo.GetHistoryByOrderIdAsync(order.Id);

        // Generate VietQR if still pending bank transfer
        BankTransferInfo? bankInfo = null;
        if (order.PaymentMethod == PaymentMethod.BankTransfer && order.PaymentStatus == PaymentStatus.Pending)
        {
            var transferContent = $"ECO {order.OrderCode}";
            var encodedAccountName = Uri.EscapeDataString(bankAccountName);
            var encodedContent = Uri.EscapeDataString(transferContent);
            var qrUrl = $"https://img.vietqr.io/image/{bankId}-{bankAccountNo}-compact2.png?amount={(long)order.TotalAmount}&addInfo={encodedContent}&accountName={encodedAccountName}";

            bankInfo = new BankTransferInfo
            {
                BankName = bankName,
                BankId = bankId,
                AccountNumber = bankAccountNo,
                AccountName = bankAccountName,
                Amount = order.TotalAmount,
                TransferContent = transferContent,
                QrCodeUrl = qrUrl
            };
        }

        return Results.Ok(new
        {
            success = true,
            order,
            payments,
            history,
            bankTransferInfo = bankInfo
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

// =========================================================================
// 5. VNPAY RETURN URL (User Browser Redirect from VNPay)
// =========================================================================
app.MapGet("/api/payment/vnpay-return", async (HttpContext ctx, IOrderRepository orderRepo) =>
{
    var query = ctx.Request.Query;
    var isValidSignature = vnpayService.ValidateSignature(query, out var orderCode, out var responseCode, out var amount);

    var isSuccess = isValidSignature && responseCode == "00";
    var transactionNo = query["vnp_TransactionNo"].ToString();
    var bankCode = query["vnp_BankCode"].ToString();

    // Record Event
    await orderRepo.RecordPaymentEventAsync(new PaymentEvent
    {
        OrderCode = orderCode,
        Provider = "VNPay_Return",
        EventName = "VNPayReturnRedirect",
        RawPayload = query.ToString() ?? string.Empty,
        IsSuccess = isSuccess,
        Note = $"ResponseCode: {responseCode}, ValidSignature: {isValidSignature}"
    });

    if (isValidSignature && !string.IsNullOrEmpty(orderCode))
    {
        if (isSuccess)
        {
            await orderRepo.UpdatePaymentStatusByCodeAsync(
                orderCode, 
                PaymentStatus.Paid, 
                transactionNo, 
                bankCode, 
                "Thanh toán thành công qua cổng VNPay Sandbox"
            );
        }
        else
        {
            await orderRepo.UpdatePaymentStatusByCodeAsync(
                orderCode, 
                PaymentStatus.Failed, 
                transactionNo, 
                bankCode, 
                $"Giao dịch VNPay không thành công (Mã lỗi: {responseCode})"
            );
        }
    }

    // Redirect to frontend result display in index.html
    var redirectUrl = $"/index.html?vnp_txn={Uri.EscapeDataString(transactionNo)}&vnp_code={Uri.EscapeDataString(responseCode)}&vnp_order={Uri.EscapeDataString(orderCode)}&vnp_success={isSuccess.ToString().ToLower()}#order-result";
    return Results.Redirect(redirectUrl);
});

// =========================================================================
// 6. VNPAY IPN WEBHOOK (Server-to-Server Instant Payment Notification)
// =========================================================================
app.MapGet("/api/payment/vnpay-ipn", async (HttpContext ctx, IOrderRepository orderRepo) =>
{
    var query = ctx.Request.Query;
    var isValidSignature = vnpayService.ValidateSignature(query, out var orderCode, out var responseCode, out var amount);

    if (!isValidSignature)
    {
        return Results.Json(new { RspCode = "97", Message = "Invalid Checksum" });
    }

    var order = await orderRepo.GetOrderByCodeAsync(orderCode);
    if (order == null)
    {
        return Results.Json(new { RspCode = "01", Message = "Order not found" });
    }

    if (order.TotalAmount != amount)
    {
        return Results.Json(new { RspCode = "04", Message = "Invalid amount" });
    }

    // Idempotency: check if order is already paid
    if (order.PaymentStatus == PaymentStatus.Paid)
    {
        return Results.Json(new { RspCode = "02", Message = "Order already confirmed" });
    }

    var transactionNo = query["vnp_TransactionNo"].ToString();
    var bankCode = query["vnp_BankCode"].ToString();

    if (responseCode == "00")
    {
        await orderRepo.UpdatePaymentStatusByCodeAsync(
            orderCode, 
            PaymentStatus.Paid, 
            transactionNo, 
            bankCode, 
            "Xác nhận thanh toán tự động qua VNPay IPN"
        );
    }
    else
    {
        await orderRepo.UpdatePaymentStatusByCodeAsync(
            orderCode, 
            PaymentStatus.Failed, 
            transactionNo, 
            bankCode, 
            $"Giao dịch VNPay thất bại theo thông báo IPN ({responseCode})"
        );
    }

    await orderRepo.RecordPaymentEventAsync(new PaymentEvent
    {
        OrderId = order.Id,
        OrderCode = order.OrderCode,
        Provider = "VNPay_IPN",
        EventName = "VNPayIPNWebhook",
        RawPayload = query.ToString() ?? string.Empty,
        IsSuccess = responseCode == "00",
        Note = $"IPN received. Result: {responseCode}"
    });

    return Results.Json(new { RspCode = "00", Message = "Confirm Success" });
});

// =========================================================================
// 7. ADMIN DASHBOARD & ORDER MANAGEMENT ENDPOINTS
// =========================================================================
app.MapGet("/api/admin/stats", async (HttpContext ctx, IOrderRepository orderRepo, AuthService authService) =>
{
    if (!IsAdminAuthorized(ctx, authService)) return Results.Unauthorized();

    var allOrders = await orderRepo.GetAllOrdersAsync();
    var totalOrders = allOrders.Count;
    var pendingOrders = allOrders.Count(o => o.OrderStatus == OrderStatus.PendingConfirmation);
    var totalRevenue = allOrders.Where(o => o.PaymentStatus == PaymentStatus.Paid).Sum(o => o.TotalAmount);
    var pendingTransfers = allOrders.Count(o => o.PaymentMethod == PaymentMethod.BankTransfer && o.PaymentStatus == PaymentStatus.Pending);
    var todayOrders = allOrders.Count(o => o.CreatedAt.Date == DateTime.UtcNow.Date);

    return Results.Ok(new
    {
        totalOrders,
        pendingOrders,
        totalRevenue,
        pendingTransfers,
        todayOrders
    });
});

app.MapGet("/api/admin/orders", async (
    HttpContext ctx,
    IOrderRepository orderRepo,
    AuthService authService,
    string? status, 
    string? paymentStatus, 
    string? paymentMethod, 
    string? search, 
    string? fromDate, 
    string? toDate) =>
{
    if (!IsAdminAuthorized(ctx, authService)) return Results.Unauthorized();

    OrderStatus? orderStatusEnum = null;
    if (Enum.TryParse<OrderStatus>(status, true, out var sVal)) orderStatusEnum = sVal;

    PaymentStatus? paymentStatusEnum = null;
    if (Enum.TryParse<PaymentStatus>(paymentStatus, true, out var psVal)) paymentStatusEnum = psVal;

    PaymentMethod? paymentMethodEnum = null;
    if (Enum.TryParse<PaymentMethod>(paymentMethod, true, out var pmVal)) paymentMethodEnum = pmVal;

    DateTime? dtFrom = null;
    if (DateTime.TryParse(fromDate, out var fVal)) dtFrom = fVal;

    DateTime? dtTo = null;
    if (DateTime.TryParse(toDate, out var tVal)) dtTo = tVal;

    var orders = await orderRepo.GetAllOrdersAsync(orderStatusEnum, paymentStatusEnum, paymentMethodEnum, search, dtFrom, dtTo);
    return Results.Ok(orders);
});

app.MapGet("/api/admin/orders/{id}", async (string id, HttpContext ctx, IOrderRepository orderRepo, AuthService authService) =>
{
    if (!IsAdminAuthorized(ctx, authService)) return Results.Unauthorized();

    var order = await orderRepo.GetOrderByIdAsync(id);
    if (order == null) return Results.NotFound(new { message = "Không tìm thấy đơn hàng." });

    var payments = await orderRepo.GetPaymentsByOrderIdAsync(order.Id);
    var history = await orderRepo.GetHistoryByOrderIdAsync(order.Id);

    return Results.Ok(new
    {
        order,
        payments,
        history
    });
});

app.MapPost("/api/admin/orders/{id}/status", async (string id, UpdateOrderStatusRequest req, HttpContext ctx, IOrderRepository orderRepo, AuthService authService) =>
{
    if (!IsAdminAuthorized(ctx, authService)) return Results.Unauthorized();

    try
    {
        var updated = await orderRepo.UpdateOrderStatusAsync(id, req.NewStatus, req.AdminName ?? "Quản trị viên", req.Note);
        if (!updated) return Results.NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });

        return Results.Ok(new { success = true, message = $"Đã cập nhật trạng thái đơn hàng sang {req.NewStatus}." });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapPost("/api/admin/orders/{id}/confirm-payment", async (string id, ConfirmPaymentRequest req, HttpContext ctx, IOrderRepository orderRepo, AuthService authService) =>
{
    if (!IsAdminAuthorized(ctx, authService)) return Results.Unauthorized();

    var txnRef = string.IsNullOrWhiteSpace(req.TransactionReference) ? $"BANK-{DateTime.UtcNow:yyyyMMddHHmmss}" : req.TransactionReference;
    var updated = await orderRepo.ConfirmPaymentAsync(id, txnRef, req.Amount, req.AdminName ?? "Quản trị viên", req.Note);
    if (!updated) return Results.NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });

    return Results.Ok(new { success = true, message = "Đã xác nhận thanh toán thành công cho đơn hàng." });
});

// =========================================================================
// 8. B2B QUOTE ENDPOINTS (Writing to SQL Server B2BQuotes table)
// =========================================================================
app.MapPost("/api/quote", async (JsonElement payload, EcoArecaDbContext db) =>
{
    try
    {
        var quote = new B2BQuote
        {
            FullName = payload.TryGetProperty("fullName", out var fn) ? fn.GetString() ?? "" : "",
            Phone = payload.TryGetProperty("phone", out var ph) ? ph.GetString() ?? "" : "",
            BusinessModel = payload.TryGetProperty("businessModel", out var bm) ? bm.GetString() ?? "" : "",
            Demand = payload.TryGetProperty("demand", out var dm) ? dm.GetString() ?? "" : "",
            Address = payload.TryGetProperty("address", out var ad) ? ad.GetString() ?? "" : "",
            QuantityExpected = payload.TryGetProperty("quantityExpected", out var qe) ? qe.GetString() ?? "" : "",
            Notes = payload.TryGetProperty("notes", out var nt) ? nt.GetString() ?? "" : "",
            ReceivedAt = DateTime.UtcNow
        };

        db.B2BQuotes.Add(quote);
        await db.SaveChangesAsync();

        return Results.Ok(new { success = true, message = "Đã tiếp nhận yêu cầu báo giá/mẫu thử thành công", id = quote.Id });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapGet("/api/quotes", async (EcoArecaDbContext db) =>
{
    var quotes = await db.B2BQuotes.OrderByDescending(q => q.ReceivedAt).ToListAsync();
    return Results.Ok(quotes);
});

Console.WriteLine("=============================================================");
Console.WriteLine("🌱 ECO-ARECA Enterprise E-Commerce & Payment Engine (.NET 8.0)");
Console.WriteLine("🌐 Server running at: http://localhost:5000");
Console.WriteLine("🗄️ Database: SQL Server (EcoArecaDb) with EF Core");
Console.WriteLine("🔒 Security: PBKDF2 Auth, State Machine, HMAC-SHA512 VNPay");
Console.WriteLine("=============================================================");

app.Run();
