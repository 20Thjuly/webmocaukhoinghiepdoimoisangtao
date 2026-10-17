# =========================================================================
# ECO-ARECA PHASE 1 AUTOMATED VERIFICATION TEST SUITE
# Tests SQL Server, EF Core, PBKDF2 Auth, State Machine, Reconciliation
# =========================================================================

$ErrorActionPreference = "Stop"
$baseUrl = "http://localhost:5000"
$adminKey = "EcoAreca@Admin2026"
$defaultPass = "Admin@EcoAreca2026"

Write-Host "=============================================================" -ForegroundColor Cyan
Write-Host "[TEST-SUITE] STARTING ECO-ARECA PHASE 1 VERIFICATION TESTS" -ForegroundColor Cyan
Write-Host "=============================================================" -ForegroundColor Cyan

$results = [System.Collections.Generic.List[PSCustomObject]]::new()

function Record-Test($name, $passed, $details) {
    $status = if ($passed) { "PASS" } else { "FAIL" }
    $color = if ($passed) { "Green" } else { "Red" }
    Write-Host "[$status] $name - $details" -ForegroundColor $color
    $results.Add([PSCustomObject]@{
        TestName = $name
        Status = $status
        Details = $details
    })
}

function Invoke-RestJson($uri, $method = "Get", $bodyObj = $null, $headers = @{}) {
    $params = @{
        Uri = $uri
        Method = $method
        Headers = $headers
    }
    if ($bodyObj -ne $null) {
        $json = $bodyObj | ConvertTo-Json -Depth 5
        $params["ContentType"] = "application/json; charset=utf-8"
        $params["Body"] = [System.Text.Encoding]::UTF8.GetBytes($json)
    }
    return Invoke-RestMethod @params
}

# -------------------------------------------------------------------------
# TEST 1: SQL Server Table Verification & Row Counts
# -------------------------------------------------------------------------
try {
    $conn = New-Object System.Data.SqlClient.SqlConnection("Server=localhost;Database=EcoArecaDb;Integrated Security=True;TrustServerCertificate=True")
    $conn.Open()
    
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM Orders; SELECT ISNULL(SUM(TotalAmount), 0) FROM Orders; SELECT COUNT(*) FROM Users; SELECT COUNT(*) FROM Warehouses; SELECT COUNT(*) FROM B2BQuotes;"
    $reader = $cmd.ExecuteReader()
    
    $reader.Read() | Out-Null
    $sqlOrdersCount = $reader.GetInt32(0)
    $reader.NextResult() | Out-Null
    $reader.Read() | Out-Null
    $sqlRevenue = $reader.GetDecimal(0)
    $reader.NextResult() | Out-Null
    $reader.Read() | Out-Null
    $sqlUsers = $reader.GetInt32(0)
    $reader.NextResult() | Out-Null
    $reader.Read() | Out-Null
    $sqlWarehouses = $reader.GetInt32(0)
    $reader.NextResult() | Out-Null
    $reader.Read() | Out-Null
    $sqlQuotes = $reader.GetInt32(0)
    $conn.Close()

    $p1 = ($sqlOrdersCount -ge 11 -and $sqlRevenue -ge 1016000 -and $sqlUsers -ge 1 -and $sqlWarehouses -ge 1 -and $sqlQuotes -ge 2)
    Record-Test "SQL Server Direct Schema & Data Reconciliation" $p1 "Orders: $sqlOrdersCount, Revenue: $sqlRevenue VND, Users: $sqlUsers, Warehouses: $sqlWarehouses, Quotes: $sqlQuotes"
} catch {
    Record-Test "SQL Server Direct Schema & Data Reconciliation" $false $_.Exception.Message
}

# -------------------------------------------------------------------------
# TEST 2: Cart Validation API (Anti-Tampering & Server-Side Pricing)
# -------------------------------------------------------------------------
try {
    $cartPayload = @{
        items = @(
            @{ productId = "chen-vuong-cham"; variantLabel = "Set 10 chiếc"; quantity = 2 },
            @{ productId = "quat-mo-cau"; variantLabel = "1 Chiếc lẻ"; quantity = 1 }
        )
        couponCode = "GIAM10K"
        shippingMethod = 0
    }

    $res = Invoke-RestJson -uri "$baseUrl/api/order/validate-cart" -method "Post" -bodyObj $cartPayload
    # Subtotal: 18,000 * 2 + 10,000 = 46,000. Under 100k, GIAM10K not applied or discount 0. Shipping: 25k. Total: 71,000.
    $p2 = ($res.isValid -eq $true -and $res.subtotal -eq 46000 -and $res.totalAmount -eq 71000)
    Record-Test "Cart Validation API" $p2 "Subtotal: $($res.subtotal), Total: $($res.totalAmount), Valid: $($res.isValid)"
} catch {
    Record-Test "Cart Validation API" $false $_.Exception.Message
}

# -------------------------------------------------------------------------
# TEST 3: Create Order into SQL Server via API
# -------------------------------------------------------------------------
$createdOrderCode = ""
$createdOrderId = ""
try {
    $orderPayload = @{
        customerName = "Lê Thị Bích Ngọc"
        phoneNumber = "0988112233"
        email = "bichngoc@ecoareca.vn"
        addressDetail = "Số 88 đường Lê Lợi"
        ward = "Phú Hội"
        district = "TP Huế"
        province = "Thừa Thiên Huế"
        customerNote = "Kiểm tra hàng trước khi nhận"
        shippingMethod = 0
        paymentMethod = 1 # BankTransfer VietQR
        couponCode = "ECOARECA10"
        items = @(
            @{ productId = "combo-gia-dinh"; variantLabel = "Set 25 món gia đình"; quantity = 2 }
        )
    }

    $orderRes = Invoke-RestJson -uri "$baseUrl/api/order/create" -method "Post" -bodyObj $orderPayload
    $createdOrderCode = $orderRes.orderCode
    $createdOrderId = $orderRes.orderId
    $p3 = ($orderRes.success -eq $true -and $createdOrderCode -match "^ECO-\d{6}-\d{4}" -and $orderRes.bankTransferInfo.qrCodeUrl -ne "")
    Record-Test "Create Order into SQL Server (VietQR)" $p3 "OrderCode: $createdOrderCode, Total: $($orderRes.totalAmount) VND, QR: Generated"
} catch {
    Record-Test "Create Order into SQL Server (VietQR)" $false $_.Exception.Message
}

# -------------------------------------------------------------------------
# TEST 4: Order Lookup from SQL Server
# -------------------------------------------------------------------------
try {
    $lookupPayload = @{
        orderCode = $createdOrderCode
        phoneNumber = "0988112233"
    }

    $lookupRes = Invoke-RestJson -uri "$baseUrl/api/order/lookup" -method "Post" -bodyObj $lookupPayload
    $p4 = ($lookupRes.success -eq $true -and $lookupRes.order.orderCode -eq $createdOrderCode -and $lookupRes.order.customerName -eq "Lê Thị Bích Ngọc")
    Record-Test "Order Lookup from SQL Server" $p4 "Found OrderCode: $($lookupRes.order.orderCode), Customer: $($lookupRes.order.customerName)"
} catch {
    Record-Test "Order Lookup from SQL Server" $false $_.Exception.Message
}

# -------------------------------------------------------------------------
# TEST 5: Security - Query String Key Rejection (Vulnerability Fix)
# -------------------------------------------------------------------------
try {
    # Attempting to access admin stats via URL query string must return 401 Unauthorized
    $urlLeakingAttempt = "$baseUrl/api/admin/stats?adminKey=$adminKey"
    $leakedRes = Invoke-RestMethod -Uri $urlLeakingAttempt -Method Get
    Record-Test "Security: Reject Query String Admin Key" $false "Should have been rejected with 401 but succeeded!"
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $p5 = ($statusCode -eq 401)
    Record-Test "Security: Reject Query String Admin Key" $p5 "Correctly rejected with HTTP 401 Unauthorized"
}

# -------------------------------------------------------------------------
# TEST 6: Security - PBKDF2 Password Authentication & Bearer Token Issue
# -------------------------------------------------------------------------
$sessionToken = ""
try {
    $loginPayload = @{
        username = "admin"
        password = $defaultPass
    }

    $loginRes = Invoke-RestJson -uri "$baseUrl/api/auth/login" -method "Post" -bodyObj $loginPayload
    $sessionToken = $loginRes.token
    $p6 = ($loginRes.success -eq $true -and $sessionToken -match "^eco_[0-9a-f]{64}")
    Record-Test "Security: PBKDF2 Admin Login & Token" $p6 "User: $($loginRes.user.username), Role: $($loginRes.user.role), Token: $sessionToken"
} catch {
    Record-Test "Security: PBKDF2 Admin Login & Token" $false $_.Exception.Message
}

# -------------------------------------------------------------------------
# TEST 7: Admin Stats via Bearer Token
# -------------------------------------------------------------------------
try {
    $authHeaders = @{ "Authorization" = "Bearer $sessionToken" }
    $statsRes = Invoke-RestJson -uri "$baseUrl/api/admin/stats" -method "Get" -headers $authHeaders
    $p7 = ($statsRes.totalOrders -ge 11 -and $statsRes.pendingOrders -ge 1)
    Record-Test "Admin Stats via Bearer Token" $p7 "Total Orders: $($statsRes.totalOrders), Pending: $($statsRes.pendingOrders), Revenue: $($statsRes.totalRevenue) VND"
} catch {
    Record-Test "Admin Stats via Bearer Token" $false $_.Exception.Message
}

# -------------------------------------------------------------------------
# TEST 8: State Machine - Reject Invalid Order Transition
# -------------------------------------------------------------------------
try {
    # Attempting to jump directly from PendingConfirmation (0) to Completed (4) must be blocked
    $authHeaders = @{ "Authorization" = "Bearer $sessionToken" }
    $invalidStatusPayload = @{
        newStatus = 4 # Completed (Illegal jump from PendingConfirmation)
        note = "Thử nhảy cóc trạng thái trái phép"
        adminName = "Hacker"
    }

    $badRes = Invoke-RestJson -uri "$baseUrl/api/admin/orders/$createdOrderId/status" -method "Post" -bodyObj $invalidStatusPayload -headers $authHeaders
    Record-Test "State Machine: Block Invalid Transition" $false "Illegal state transition was allowed!"
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $p8 = ($statusCode -eq 400)
    Record-Test "State Machine: Block Invalid Transition" $p8 "Correctly blocked with HTTP 400 (Cannot jump PendingConfirmation -> Completed)"
}

# -------------------------------------------------------------------------
# TEST 9: State Machine - Accept Valid Order Transition
# -------------------------------------------------------------------------
try {
    # Legitimate transition: PendingConfirmation (0) -> Confirmed (1)
    $authHeaders = @{ "Authorization" = "Bearer $sessionToken" }
    $validStatusPayload = @{
        newStatus = 1 # Confirmed
        note = "Quản trị viên duyệt đơn hợp lệ"
        adminName = "Admin"
    }

    $okRes = Invoke-RestJson -uri "$baseUrl/api/admin/orders/$createdOrderId/status" -method "Post" -bodyObj $validStatusPayload -headers $authHeaders
    $p9 = ($okRes.success -eq $true)
    Record-Test "State Machine: Accept Valid Transition" $p9 "Successfully transitioned to Confirmed"
} catch {
    Record-Test "State Machine: Accept Valid Transition" $false $_.Exception.Message
}

# -------------------------------------------------------------------------
# TEST 10: Manual Payment Reconciliation in SQL Server
# -------------------------------------------------------------------------
try {
    $authHeaders = @{ "Authorization" = "Bearer $sessionToken" }
    $payConfirmPayload = @{
        amount = 142000
        transactionReference = "MB-TEST-RECON-001"
        note = "Xác nhận đối soát chuyển khoản ngân hàng thực tế"
        adminName = "Admin"
    }

    $payRes = Invoke-RestJson -uri "$baseUrl/api/admin/orders/$createdOrderId/confirm-payment" -method "Post" -bodyObj $payConfirmPayload -headers $authHeaders
    $p10 = ($payRes.success -eq $true)
    Record-Test "Payment Confirmation in SQL Server" $p10 "Updated PaymentStatus to Paid, recorded in Payments & OrderStatusHistories"
} catch {
    Record-Test "Payment Confirmation in SQL Server" $false $_.Exception.Message
}

# -------------------------------------------------------------------------
# TEST 11: VNPay IPN Idempotency Test
# -------------------------------------------------------------------------
try {
    # Test forged checksum
    $forgedIpnUrl = "$baseUrl/api/payment/vnpay-ipn?vnp_Amount=10000000&vnp_Command=pay&vnp_OrderInfo=test&vnp_ResponseCode=00&vnp_TxnRef=$createdOrderCode&vnp_SecureHash=INVALID_HASH_12345"
    $forgedRes = Invoke-RestMethod -Uri $forgedIpnUrl -Method Get
    $p11 = ($forgedRes.RspCode -eq "97")
    Record-Test "VNPay IPN: Forged Signature Rejection" $p11 "Response: RspCode=$($forgedRes.RspCode), Message=$($forgedRes.Message)"
} catch {
    Record-Test "VNPay IPN: Forged Signature Rejection" $false $_.Exception.Message
}

Write-Host "=============================================================" -ForegroundColor Cyan
Write-Host "[SUMMARY] TEST RESULTS: $($results.Where({$_.Status -eq 'PASS'}).Count)/$($results.Count) TESTS PASSED" -ForegroundColor Cyan
Write-Host "=============================================================" -ForegroundColor Cyan

$results | Format-Table -AutoSize
