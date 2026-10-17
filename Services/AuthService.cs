using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.EntityFrameworkCore;
using EcoAreca.Data;
using EcoAreca.Models;

namespace EcoAreca.Services;

public class AuthService
{
    private readonly EcoArecaDbContext _db;
    private static readonly ConcurrentDictionary<string, (string UserId, string Username, string Role, DateTime ExpiresAt)> _activeSessions = new();

    public AuthService(EcoArecaDbContext db)
    {
        _db = db;
    }

    public static (string Hash, string Salt) HashPassword(string password)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(16); // 128-bit salt
        string salt = Convert.ToBase64String(saltBytes);

        byte[] hashBytes = KeyDerivation.Pbkdf2(
            password: password,
            salt: saltBytes,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 32 // 256-bit subkey
        );
        string hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        try
        {
            byte[] saltBytes = Convert.FromBase64String(storedSalt);
            byte[] computedHashBytes = KeyDerivation.Pbkdf2(
                password: password,
                salt: saltBytes,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 32
            );
            string computedHash = Convert.ToBase64String(computedHashBytes);

            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(computedHash),
                Convert.FromBase64String(storedHash)
            );
        }
        catch
        {
            return false;
        }
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResponse { Success = false, Message = "Tên đăng nhập và mật khẩu không được để trống." };
        }

        var cleanUsername = username.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername);

        if (user == null || !user.IsActive)
        {
            return new LoginResponse { Success = false, Message = "Tài khoản không tồn tại hoặc đã bị khóa." };
        }

        if (!VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
        {
            return new LoginResponse { Success = false, Message = "Mật khẩu không chính xác." };
        }

        // Generate cryptographically secure session token (256-bit)
        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
        string token = "eco_" + Convert.ToHexString(tokenBytes).ToLowerInvariant();
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);

        _activeSessions[token] = (user.Id, user.Username, user.Role, expiresAt);

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new LoginResponse
        {
            Success = true,
            Message = "Đăng nhập thành công.",
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role
            }
        };
    }

    public bool ValidateSessionToken(string token, out string username, out string role)
    {
        username = string.Empty;
        role = string.Empty;

        if (string.IsNullOrWhiteSpace(token)) return false;

        var cleanToken = token.Trim();
        if (cleanToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            cleanToken = cleanToken[7..].Trim();
        }

        if (_activeSessions.TryGetValue(cleanToken, out var session))
        {
            if (session.ExpiresAt > DateTime.UtcNow)
            {
                username = session.Username;
                role = session.Role;
                return true;
            }
            else
            {
                _activeSessions.TryRemove(cleanToken, out _);
            }
        }

        return false;
    }

    public async Task EnsureAdminSeededAsync(string defaultAdminUsername = "admin", string defaultPassword = "Admin@EcoAreca2026")
    {
        var existing = await _db.Users.AnyAsync();
        if (!existing)
        {
            var (hash, salt) = HashPassword(defaultPassword);
            var adminUser = new User
            {
                Username = defaultAdminUsername,
                PasswordHash = hash,
                PasswordSalt = salt,
                FullName = "Quản Trị Viên Hệ Thống",
                Email = "admin@ecoareca.vn",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(adminUser);
            await _db.SaveChangesAsync();
            Console.WriteLine($"[AuthService] Seeded initial admin account: '{defaultAdminUsername}'");
        }
    }
}
