using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Rxnxt.Business.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Rxnxt.Services.Implementations;

public sealed class AuthService
{
    private readonly PharmacyDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthService(PharmacyDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<UserMasterRow?> ValidateUserAsync(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await _db.UserMasters
            .FirstOrDefaultAsync(u =>
                u.UserName == userName &&
                u.ActiveStatus &&
                u.Password == password);

        return user;
    }

    public string GenerateJwt(UserMasterRow user)
    {
        var secret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT secret not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserID ?? ""),
            new Claim(ClaimTypes.Name, user.UserName ?? ""),
            new Claim(ClaimTypes.GivenName, user.FirstName ?? ""),
            new Claim(ClaimTypes.Role, user.UserGroup ?? ""),
            new Claim("TenantID", user.TenantId ?? ""),
            new Claim("UserID", user.UserID ?? "")
        };

        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var parsed) ? parsed : 180;

        var token = new JwtSecurityToken(
            issuer: "RxnxtApp",
            audience: "RxnxtApp",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
