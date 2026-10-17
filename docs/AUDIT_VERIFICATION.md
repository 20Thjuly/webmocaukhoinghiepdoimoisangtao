# ECO-ARECA — BÁO CÁO ĐỐI CHIẾU & XÁC MINH AUDIT MÃ NGUỒN THỰC TẾ
**Dự án:** ECO-ARECA — Website thương mại điện tử chén dĩa mo cau  
**Chuyên gia thực hiện:** Senior .NET Architect, Database Engineer & Application Security Engineer  
**Ngày thực hiện đối chiếu:** 17/10/2026  
**Môi trường kiểm tra:** Windows 11, .NET SDK 8.0.404, Microsoft SQL Server 2022 (Developer Edition, v16.0.1000), Powershell 5.1  
**Tài liệu đối chiếu gốc:** [ENTERPRISE_AUDIT_ROADMAP.md](../ENTERPRISE_AUDIT_ROADMAP.md)

---

## 1. PHƯƠNG PHÁP & NGUYÊN TẮC XÁC MINH

Báo cáo này được thực hiện dựa trên nguyên tắc **Mã nguồn và Kết quả thực thi thực tế là chân lý tối thượng** (Source Code & Runtime Execution are Ground Truth). Không một giả định hay kết luận nào từ tài liệu cũ được chấp nhận nếu không có bằng chứng code hoặc kiểm thử thực nghiệm trên hệ thống đang chạy.

### Phân loại trạng thái đối chiếu:
1. **[Xác minh bằng mã nguồn]**: Đã định vị chính xác file, hàm, dòng lệnh thực thi logic nghiệp vụ.
2. **[Xác minh bằng kiểm thử thực tế]**: Đã chạy server Kestrel, gọi REST API/script PowerShell hoặc truy vấn SQL Server trực tiếp và ghi nhận kết quả.
3. **[Chưa đủ bằng chứng]**: Các tuyên bố trong báo cáo cũ mang tính phỏng đoán, quy kết chung chung mà không chỉ ra được dòng code hoặc kịch bản cụ thể.
4. **[Sai hoặc cần chỉnh sửa]**: Các nhận định trong báo cáo cũ không phản ánh đúng logic mã nguồn thực tế hoặc đề xuất giải pháp chưa chặt chẽ, có thể gây sập hệ thống.

---

## 2. BẢNG ĐỐI CHIẾU TOÀN DIỆN TỪNG NHẬN ĐỊNH CỦA BÁO CÁO CŨ

| STT | Module / Nội dung nhận định trong báo cáo cũ | Trạng thái xác minh | Vị trí file & Dòng code thực tế | Bằng chứng kỹ thuật & Đánh giá chi tiết |
|---|---|---|---|---|
| **1** | **Xác thực Admin bằng Static Key**<br>Báo cáo cũ kết luận: Khóa admin bị hardcode cố định, bất kỳ ai có link `?adminKey=` đều xem và sửa được dữ liệu. | **Đã xác minh bằng kiểm thử thực tế** | `Program.cs` (L108-L126 cũ), `js/admin.js` (L12-L21) | **XÁC NHẬN NGUY CƠ CAO:** Mã nguồn cũ nhận `adminKey` qua cả query string `?adminKey=...`, lưu trong `localStorage['eco_admin_key']`. Kiểm thử thực tế cho thấy query string này bị lưu vết trong browser history, proxy log và Kestrel access log.<br>*Hành động Đợt 1*: Đã loại bỏ hoàn toàn việc nhận key qua query string; bổ sung xác thực PBKDF2 với muối 128-bit và token phiên 256-bit. |
| **2** | **Lưu trữ dữ liệu bằng Flat-File JSON**<br>Báo cáo cũ kết luận: 5 file JSON trong thư mục `data/` không có ACID, nguy cơ race condition và hỏng file khi có ghi đồng thời. | **Đã xác minh bằng mã nguồn & kiểm thử** | `data/orders.json`, `data/payments.json`, `Program.cs` | **XÁC NHẬN:** Hệ thống cũ dùng `File.WriteAllTextAsync` không có transaction lock. Đặc biệt, kiểm tra thực tế phát hiện trong `data/orders.json` có 2 bản ghi rác (OrderCode rỗng, TotalAmount = 0). Khi nạp vào SQL Server với Unique Constraint, lỗi vi phạm ràng buộc lập tức được phát hiện.<br>*Hành động Đợt 1*: Di chuyển toàn bộ sang Microsoft SQL Server qua EF Core 8. |
| **3** | **Kiểm tra giỏ hàng & Tính giá phía Server**<br>Báo cáo cũ ghi nhận: Đơn giá và voucher được xác thực phía máy chủ, không tin tưởng giá client. | **Đã xác minh bằng kiểm thử thực tế** | `Services/ProductCatalogService.cs` (L142-L257), `Program.cs` (L176-L200) | **CHÍNH XÁC:** API `/api/order/validate-cart` và `/api/order/create` đều gọi `ProductCatalogService.ValidateAndCalculateCart`. Client gửi giá giả mạo 1đ đều bị ghi đè bằng giá niêm yết trong server catalog. Test Case 2 chạy thực tế: Subtotal 46,000 VND + Ship 25,000 VND = 71,000 VND (PASS). |
| **4** | **Bán hàng vượt tồn kho (Overselling)**<br>Báo cáo cũ kết luận: Hệ thống cho phép đặt hàng vô tận vì không có quản lý kho. | **Đã xác minh bằng mã nguồn** | `Services/ProductCatalogService.cs`, `Program.cs` | **CHÍNH XÁC:** Entity sản phẩm chỉ có cờ `IsAvailable = true`, không có trường số lượng tồn vật lý (`PhysicalQuantity`) hay tồn giữ chỗ (`ReservedQuantity`). Đặt 10,000 bộ combo hệ thống vẫn tạo đơn bình thường.<br>*Hành động Đợt 1*: Thiết kế schema `Warehouses`, `ProductInventories`, `StockMovements`. |
| **5** | **Nhảy cóc trạng thái đơn hàng (State Machine)**<br>Báo cáo cũ nhận định: Admin có thể tùy tiện chuyển đơn từ Chờ xác nhận sang Đã hoàn thành hoặc Đã hủy sang Đã giao. | **Đã xác minh bằng mã nguồn & kiểm thử** | `Program.cs` (L433-L460 cũ), `js/admin.js` | **XÁC NHẬN:** Endpoint cũ `/api/admin/orders/{id}/status` gán trực tiếp `order.OrderStatus = (OrderStatus)request.NewStatus` mà không kiểm tra quy tắc chuyển trạng thái hợp lệ.<br>*Hành động Đợt 1*: Triển khai `OrderStateMachine.CanTransition()`. Test Case 8 chặn chuyển từ `PendingConfirmation` (0) sang `Completed` (4) với HTTP 400 (PASS). |
| **6** | **Chống trùng Webhook VNPay (Idempotency)**<br>Báo cáo cũ nêu: VNPay IPN có thể bị xử lý nhiều lần nếu đối tác retry. | **Sai / Cần chỉnh sửa** | `Services/VNPayService.cs`, `Program.cs` (L380-L425) | **CẦN CHỈNH SỬA:** Báo cáo cũ cho rằng hệ thống chưa có idempotency. Thực tế đọc code `Program.cs` cho thấy đã có đoạn kiểm tra: `if (existingPayment.PaymentStatus == PaymentStatus.Paid) return Results.Ok(new VNPayIpnResponse { RspCode = "02", Message = "Order already confirmed" });`. Tuy nhiên, vì lưu file JSON nên tồn tại time-of-check to time-of-use (TOCTOU). Trên SQL Server Đợt 1, logic này được bọc trong Serializable/ACID transaction. |
| **7** | **Xác thực chữ ký VNPay (Checksum HMAC-SHA512)**<br>Báo cáo cũ ghi nhận: VNPay IPN có xác thực chữ ký an toàn. | **Đã xác minh bằng kiểm thử thực tế** | `Services/VNPayService.cs` (L75-L115), `Program.cs` (L365-L380) | **CHÍNH XÁC:** Test Case 11 gửi gói tin IPN với chữ ký giả mạo `INVALID_HASH_12345`. Hệ thống ngay lập tức từ chối với mã phản hồi `RspCode = "97"` (Invalid Checksum). |
| **8** | **Tra cứu đơn hàng (Order Lookup)**<br>Báo cáo cũ nhận định: Tra cứu yêu cầu cả mã đơn và số điện thoại, bảo vệ thông tin cá nhân. | **Đã xác minh bằng kiểm thử thực tế** | `Program.cs` (L289-L345), `js/order-lookup.js` | **CHÍNH XÁC:** Test Case 4 tra cứu đơn `ECO-261017-9718` với số điện thoại `0988112233` thành công, che giấu các thông tin nhạy cảm của khách hàng khác. |
| **9** | **Nhận định về kiến trúc Microservices / AI Chatbot**<br>Báo cáo cũ đề xuất: Tách Microservices và tích hợp Semantic Kernel AI ngay trong Đợt 1. | **Sai / Thiết kế chưa chặt** | Toàn bộ codebase | **BÁC BỎ ĐỀ XUẤT CŨ:** Với quy mô website doanh nghiệp vừa và nhỏ, việc chia tách microservices gây over-engineering nghiêm trọng, tăng chi phí vận hành mạng và quản lý phân tán. Đề xuất đưa AI thực thi SQL tùy ý là lỗ hổng an ninh thảm họa. Phương án chuẩn hóa là **Modular Monolith** và đóng băng AI cho đến khi dữ liệu chuẩn hóa. |

---

## 3. CÁC VẤN ĐỀ BẢO MẬT ĐÃ PHÁT HIỆN & XỬ LÝ TRONG ĐỢT 1

### 3.1. Loại bỏ rò rỉ Khóa Quản trị qua URL Query String
- **Thực trạng cũ:** Hàm `IsAdminAuthorized` chấp nhận `context.Request.Query["adminKey"]`. Trong `admin.html`, JavaScript gửi key trực tiếp trên link tải danh sách đơn và thống kê.
- **Rủi ro:** Khi admin chia sẻ link hoặc xem từ mạng công cộng, key bị lưu lại trên Web Server log, Browser history, Referer header.
- **Biện pháp đã xử lý:** Loại bỏ hoàn toàn `Query["adminKey"]`. Chỉ chấp nhận Header `Authorization: Bearer <token>` hoặc header nội bộ `X-Admin-Key`.

### 3.2. Chuyển đổi Cơ chế Xác thực sang PBKDF2 & Token phiên động
- **Thực trạng cũ:** Dùng static secret string chia sẻ chung. Không có tài khoản cá nhân, không có audit trail ai là người sửa đơn.
- **Biện pháp đã xử lý:** 
  - Tạo bảng `Users` trong SQL Server.
  - Sử dụng thuật toán chuẩn công nghiệp `KeyDerivation.Pbkdf2` với HMAC-SHA256, 100,000 vòng lặp (iterations) và muối ngẫu nhiên 128-bit (`Rfc2898DeriveBytes`).
  - Cấp phát Secure Token ngẫu nhiên 256-bit dạng `eco_<hex64>` lưu trong bộ nhớ phiên với thời hạn hết hạn (TTL 24 giờ).

### 3.3. Xử lý dữ liệu rác & Vi phạm tính toàn vẹn (Data Corruption)
- **Thực trạng cũ:** File `data/orders.json` có các bản ghi trống cuối file do ngắt kết nối đột ngột hoặc script test cũ.
- **Biện pháp đã xử lý:** Trong `DataMigrationService.cs`, bổ sung bộ lọc làm sạch (Sanitization Filter): loại bỏ bản ghi không có mã đơn hợp lệ trước khi ghi vào SQL Server, đảm bảo 100% bản ghi chuyển đổi đều hợp lệ và toàn vẹn.

---

## 4. KẾT LUẬN GIAI ĐOẠN A
Báo cáo audit cũ phản ánh đúng khoảng 75% hiện trạng nhưng mắc lỗi quy kết vội vã ở một số điểm (như VNPay Idempotency) và đề xuất phương án kiến trúc quá phức tạp (Microservices, AI tùy tiện). 

Phương án tiếp cận của Đợt 1 là **giữ vững hợp đồng API hiện tại, bảo toàn 100% trải nghiệm người dùng frontend, chuyển đổi nền tảng sang SQL Server + EF Core 8.0 và thiết lập Modular Monolith**.
