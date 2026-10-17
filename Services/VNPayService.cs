using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace EcoAreca.Services;

public class VNPayConfig
{
    public string TmnCode { get; set; } = "ECOARECA"; // Test Terminal Code
    public string HashSecret { get; set; } = "RAK3165VZQUQZKGJAXWQRZ88WQQ6L1VO"; // Test Hash Secret
    public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string ReturnUrl { get; set; } = "http://localhost:5000/api/payment/vnpay-return";
}

public class VNPayService
{
    private readonly VNPayConfig _config;

    public VNPayService(VNPayConfig config)
    {
        _config = config;
    }

    public string CreatePaymentUrl(string orderCode, decimal amount, string orderInfo, string ipAddress)
    {
        var vnpParams = new SortedDictionary<string, string>(new VNPayCompare());

        vnpParams.Add("vnp_Version", "2.1.0");
        vnpParams.Add("vnp_Command", "pay");
        vnpParams.Add("vnp_TmnCode", _config.TmnCode);
        vnpParams.Add("vnp_Amount", ((long)(amount * 100)).ToString()); // VNPay multiplier x100
        vnpParams.Add("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
        vnpParams.Add("vnp_CurrCode", "VND");
        vnpParams.Add("vnp_IpAddr", string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress);
        vnpParams.Add("vnp_Locale", "vn");
        vnpParams.Add("vnp_OrderInfo", string.IsNullOrWhiteSpace(orderInfo) ? $"Thanh toan don hang {orderCode}" : orderInfo);
        vnpParams.Add("vnp_OrderType", "other");
        vnpParams.Add("vnp_ReturnUrl", _config.ReturnUrl);
        vnpParams.Add("vnp_TxnRef", orderCode);

        // Build data to hash & query string
        var signData = new StringBuilder();
        var queryString = new StringBuilder();

        foreach (var (key, value) in vnpParams)
        {
            if (!string.IsNullOrEmpty(value))
            {
                var encodedKey = WebUtility.UrlEncode(key);
                var encodedValue = WebUtility.UrlEncode(value);

                if (signData.Length > 0)
                {
                    signData.Append('&');
                    queryString.Append('&');
                }

                signData.Append(encodedKey).Append('=').Append(encodedValue);
                queryString.Append(encodedKey).Append('=').Append(encodedValue);
            }
        }

        var secureHash = HmacSha512(_config.HashSecret, signData.ToString());
        queryString.Append("&vnp_SecureHash=").Append(secureHash);

        return $"{_config.BaseUrl}?{queryString}";
    }

    public bool ValidateSignature(IQueryCollection query, out string orderCode, out string responseCode, out decimal amount)
    {
        orderCode = string.Empty;
        responseCode = string.Empty;
        amount = 0;

        var vnpParams = new SortedDictionary<string, string>(new VNPayCompare());
        string receivedHash = string.Empty;

        foreach (var (key, value) in query)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith("vnp_")) continue;

            if (key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase))
            {
                receivedHash = value.ToString();
            }
            else if (!key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                vnpParams.Add(key, value.ToString());
            }
        }

        if (string.IsNullOrEmpty(receivedHash)) return false;

        var signData = new StringBuilder();
        foreach (var (key, value) in vnpParams)
        {
            if (!string.IsNullOrEmpty(value))
            {
                var encodedKey = WebUtility.UrlEncode(key);
                var encodedValue = WebUtility.UrlEncode(value);

                if (signData.Length > 0)
                {
                    signData.Append('&');
                }

                signData.Append(encodedKey).Append('=').Append(encodedValue);
            }
        }

        var calculatedHash = HmacSha512(_config.HashSecret, signData.ToString());
        var isSignatureValid = calculatedHash.Equals(receivedHash, StringComparison.OrdinalIgnoreCase);

        if (vnpParams.TryGetValue("vnp_TxnRef", out var txnRef)) orderCode = txnRef;
        if (vnpParams.TryGetValue("vnp_ResponseCode", out var respCode)) responseCode = respCode;
        if (vnpParams.TryGetValue("vnp_Amount", out var rawAmt) && long.TryParse(rawAmt, out var amtVal))
        {
            amount = amtVal / 100m;
        }

        return isSignatureValid;
    }

    private static string HmacSha512(string key, string inputData)
    {
        var hash = new StringBuilder();
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);

        using (var hmac = new HMACSHA512(keyBytes))
        {
            var hashValue = hmac.ComputeHash(inputBytes);
            foreach (var b in hashValue)
            {
                hash.Append(b.ToString("x2"));
            }
        }

        return hash.ToString();
    }

    private class VNPayCompare : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            var vnpCompare = CompareInfo.GetCompareInfo("en-US");
            return vnpCompare.Compare(x, y, CompareOptions.Ordinal);
        }
    }
}
