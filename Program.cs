using System.Text.Json;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Configure port
builder.WebHost.UseUrls("http://localhost:5000");

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

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

// Ensure data directory exists
var dataDir = Path.Combine(contentRoot, "data");
if (!Directory.Exists(dataDir))
{
    Directory.CreateDirectory(dataDir);
}

var quotesFilePath = Path.Combine(dataDir, "quotes.json");
var ordersFilePath = Path.Combine(dataDir, "orders.json");

// Helper method to append JSON records
async Task SaveRecordAsync(string filePath, object newRecord)
{
    List<object> list = new();
    if (File.Exists(filePath))
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var existing = JsonSerializer.Deserialize<List<object>>(content);
            if (existing != null) list = existing;
        }
        catch { }
    }
    list.Add(newRecord);
    var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(filePath, json);
}

// B2B Quote / Sample Request Endpoint
app.MapPost("/api/quote", async (JsonElement payload) =>
{
    try
    {
        var record = new
        {
            id = Guid.NewGuid().ToString("N")[..8],
            receivedAt = DateTime.UtcNow.ToString("o"),
            data = payload
        };
        await SaveRecordAsync(quotesFilePath, record);
        return Results.Ok(new { success = true, message = "Đã tiếp nhận yêu cầu báo giá/mẫu thử thành công", id = record.id });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

// Retail & Combo Order Checkout Endpoint
app.MapPost("/api/order", async (JsonElement payload) =>
{
    try
    {
        var record = new
        {
            id = Guid.NewGuid().ToString("N")[..8],
            receivedAt = DateTime.UtcNow.ToString("o"),
            data = payload
        };
        await SaveRecordAsync(ordersFilePath, record);
        return Results.Ok(new { success = true, message = "Đã lưu đơn hàng thành công", id = record.id });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

// Admin endpoints
app.MapGet("/api/quotes", async () =>
{
    if (!File.Exists(quotesFilePath)) return Results.Ok(Array.Empty<object>());
    var json = await File.ReadAllTextAsync(quotesFilePath);
    return Results.Content(json, "application/json");
});

app.MapGet("/api/orders", async () =>
{
    if (!File.Exists(ordersFilePath)) return Results.Ok(Array.Empty<object>());
    var json = await File.ReadAllTextAsync(ordersFilePath);
    return Results.Content(json, "application/json");
});

Console.WriteLine("=================================================");
Console.WriteLine("🌱 ECO-ARECA Server running at: http://localhost:5000");
Console.WriteLine("=================================================");

app.Run();
