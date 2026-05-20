using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;

namespace Ramza_EBike_Swabi.Services
{
    public class AuthService
    {
        public async Task<(bool Success, string? Error)> RegisterAsync(string username, string password, string fullName, string role)
        {
            using var db = new AppDbContext();

            username = username?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
                return (false, "All fields are required.");

            if (await db.Users.AnyAsync(u => u.Username == username))
                return (false, "Username already exists.");

            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            db.Users.Add(new User
            {
                Username = username,
                PasswordHash = hash,
                FullName = fullName.Trim(),
                Role = role
            });
            await db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<User?> LoginAsync(string username, string password)
        {
            using var db = new AppDbContext();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (user == null) return null;
            return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
        }
    }
}
