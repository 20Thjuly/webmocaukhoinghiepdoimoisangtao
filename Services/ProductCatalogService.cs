using EcoAreca.Models;

namespace EcoAreca.Services;

public class ServerProductVariant
{
    public string Label { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class ServerProduct
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Dimensions { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<ServerProductVariant> Variants { get; set; } = new();
}

public class ProductCatalogService
{
    public const decimal FreeShippingThreshold = 300000m; // 300.000đ freeship
    public const decimal DefaultShippingFee = 25000m;     // 25.000đ phí giao hàng

    private readonly List<ServerProduct> _catalog = new()
    {
        new ServerProduct
        {
            Id = "chen-vuong-cham",
            Name = "Chén Vuông Chấm Gia Vị",
            Category = "chen",
            Dimensions = "7.5 × 7.5 × 2 cm",
            Image = "assets/images/chen-vuong-cham.jpg",
            Variants = new()
            {
                new() { Label = "Set 10 chiếc", Price = 18000m },
                new() { Label = "Set 25 chiếc", Price = 42000m },
                new() { Label = "Set 50 chiếc", Price = 80000m }
            }
        },
        new ServerProduct
        {
            Id = "chen-tron-sup",
            Name = "Chén Tròn Súp / Chè",
            Category = "chen",
            Dimensions = "Đường kính 12 cm — Sâu 4 cm",
            Image = "assets/images/chen-tron-sup.jpg",
            Variants = new()
            {
                new() { Label = "Set 10 chiếc", Price = 30000m },
                new() { Label = "Set 25 chiếc", Price = 70000m },
                new() { Label = "Set 50 chiếc", Price = 135000m }
            }
        },
        new ServerProduct
        {
            Id = "dia-tron-mo-cau",
            Name = "Dĩa Tròn Mo Cau Tự Nhiên",
            Category = "dia-tron",
            Dimensions = "15 cm, 20 cm, 25 cm",
            Image = "assets/images/dia-tron-mo-cau.jpg",
            Variants = new()
            {
                new() { Label = "Dĩa 15 cm (Set 10)", Price = 35000m },
                new() { Label = "Dĩa 20 cm (Set 10)", Price = 45000m },
                new() { Label = "Dĩa 25 cm (Set 10)", Price = 55000m }
            }
        },
        new ServerProduct
        {
            Id = "dia-vuong-bbq",
            Name = "Dĩa Vuông / Khay BBQ Mo Cau",
            Category = "dia-vuong",
            Dimensions = "15 × 15 cm, 20 × 20 cm, 18 × 24 cm",
            Image = "assets/images/dia-vuong-bbq.jpg",
            Variants = new()
            {
                new() { Label = "15 × 15 cm (Set 10)", Price = 39000m },
                new() { Label = "20 × 20 cm (Set 10)", Price = 52000m },
                new() { Label = "18 × 24 cm (Set 10)", Price = 65000m }
            }
        },
        new ServerProduct
        {
            Id = "combo-gia-dinh",
            Name = "Combo Gia Đình Tiện Lợi",
            Category = "combo",
            Dimensions = "Bộ tổng hợp đa kích thước",
            Image = "assets/images/combo-gia-dinh.jpg",
            Variants = new()
            {
                new() { Label = "Set 10 món tổng hợp", Price = 65000m },
                new() { Label = "Set 25 món gia đình", Price = 145000m }
            }
        },
        new ServerProduct
        {
            Id = "quat-mo-cau",
            Name = "Quạt Mo Cau Tròn Liền Cán",
            Category = "quat",
            Dimensions = "22 × 36 cm (Nguyên khối liền cán)",
            Image = "assets/images/quat-mo-cau.png",
            Variants = new()
            {
                new() { Label = "1 Chiếc lẻ", Price = 10000m },
                new() { Label = "Set 10 chiếc (9k/c)", Price = 90000m },
                new() { Label = "Set 20 chiếc (8k/c)", Price = 160000m },
                new() { Label = "Set 50 chiếc (7k/c)", Price = 350000m }
            }
        },
        new ServerProduct
        {
            Id = "quat-khac-laser-hue",
            Name = "Quạt Mo Cau Khắc Laser Huế / Theo Yêu Cầu",
            Category = "quat",
            Dimensions = "22 × 36 cm (Khắc laser tinh xảo)",
            Image = "assets/images/quat-khac-laser-hue.jpg",
            Variants = new()
            {
                new() { Label = "1 Chiếc lẻ", Price = 15000m },
                new() { Label = "Set 10 chiếc (13.5k/c)", Price = 135000m },
                new() { Label = "Set 20 chiếc (12k/c)", Price = 240000m },
                new() { Label = "Set 50 chiếc (11k/c)", Price = 550000m }
            }
        }
    };

    public List<ServerProduct> GetAllProducts() => _catalog;

    public ServerProduct? GetProductById(string id)
    {
        return _catalog.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && p.IsActive);
    }

    /// <summary>
    /// Recalculates and validates all items, quantities, coupons, and shipping fees authoritatively on the server.
    /// Never trust prices sent from the client!
    /// </summary>
    public ValidatedCartResponse ValidateAndCalculateCart(List<CartItemRequest> items, string? couponCode, ShippingMethod shippingMethod)
    {
        var response = new ValidatedCartResponse();

        if (items == null || items.Count == 0)
        {
            response.IsValid = false;
            response.ErrorMessage = "Giỏ hàng không có sản phẩm nào.";
            return response;
        }

        var validatedItems = new List<OrderItem>();
        decimal subtotal = 0;

        foreach (var reqItem in items)
        {
            if (reqItem.Quantity <= 0)
            {
                response.IsValid = false;
                response.ErrorMessage = "Số lượng sản phẩm không hợp lệ (phải lớn hơn 0).";
                return response;
            }

            var product = GetProductById(reqItem.ProductId);
            if (product == null)
            {
                response.IsValid = false;
                response.ErrorMessage = $"Sản phẩm mã '{reqItem.ProductId}' không tồn tại hoặc đã ngừng kinh doanh.";
                return response;
            }

            var variant = product.Variants.FirstOrDefault(v => v.Label.Equals(reqItem.VariantLabel, StringComparison.OrdinalIgnoreCase))
                          ?? product.Variants.FirstOrDefault();

            if (variant == null || !variant.IsAvailable)
            {
                response.IsValid = false;
                response.ErrorMessage = $"Quy cách '{reqItem.VariantLabel}' của sản phẩm '{product.Name}' hiện không khả dụng.";
                return response;
            }

            var orderItem = new OrderItem
            {
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                VariantSnapshot = variant.Label,
                ImageSnapshot = product.Image,
                UnitPriceSnapshot = variant.Price,
                Quantity = reqItem.Quantity
            };

            validatedItems.Add(orderItem);
            subtotal += orderItem.LineTotal;
        }

        response.ValidatedItems = validatedItems;
        response.Subtotal = subtotal;

        // Calculate Discount
        decimal discount = 0;
        string? cleanCoupon = couponCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(cleanCoupon))
        {
            if (cleanCoupon == "ECOARECA10")
            {
                // 10% discount on subtotal, up to 50,000 VND
                discount = Math.Min(subtotal * 0.10m, 50000m);
                response.AppliedCoupon = cleanCoupon;
            }
            else if (cleanCoupon == "GIAM10K")
            {
                // 10,000 VND discount for orders from 100,000 VND
                if (subtotal >= 100000m)
                {
                    discount = 10000m;
                    response.AppliedCoupon = cleanCoupon;
                }
            }
            else if (cleanCoupon == "FREESHIP")
            {
                // Free ship
                response.AppliedCoupon = cleanCoupon;
            }
        }
        response.DiscountAmount = Math.Round(discount, 0);

        // Calculate Shipping Fee
        decimal shippingFee = 0;
        if (shippingMethod == ShippingMethod.ShowroomPickup)
        {
            shippingFee = 0; // Self pickup at workshop
        }
        else
        {
            if (subtotal >= FreeShippingThreshold || cleanCoupon == "FREESHIP")
            {
                shippingFee = 0;
            }
            else
            {
                shippingFee = DefaultShippingFee;
            }
        }

        response.ShippingFee = shippingFee;
        response.IsFreeShipping = shippingFee == 0;
        response.FreeShippingThresholdRemaining = subtotal >= FreeShippingThreshold 
            ? 0 
            : Math.Max(0, FreeShippingThreshold - subtotal);

        // Final total amount
        response.TotalAmount = Math.Max(0, subtotal - response.DiscountAmount + shippingFee);
        response.IsValid = true;

        return response;
    }
}
