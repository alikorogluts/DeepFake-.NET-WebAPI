using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Deepfake.API.Controllers;

[ApiController]
[Route("api/v1/auth")] // Versiyonlama eklendi
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    public AuthController(IConfiguration config) => _config = config;

    [HttpGet]
    [Authorize] // Sadece geçerli token'ı olanlar girebilir
    public IActionResult GetStatus()
    {
        // Token geçerliyse buraya düşer
        return Ok(new { isAuthenticated = true });
    }

    [HttpPost]
    public IActionResult GenerateToken(
        [FromHeader(Name = "X-Client-Token")] string? clientToken,
        [FromHeader(Name = "X-Client-Platform")]
        string platform = "web")
    {
        var expectedToken = Environment.GetEnvironmentVariable("JWT_CLIENT_TOKEN");

        if (clientToken != expectedToken) return Unauthorized(new { message = "Geçersiz istemci." });

        // 1. IP ALMA VE TEMİZLEME (::ffff: gibi ön ekleri siliyoruz)
        var rawIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var cleanIp = rawIp.Replace("::ffff:", "");
        if (cleanIp == "::1") cleanIp = "127.0.0.1";

        // 📝 DEBUG LOG: Token üretilirken IP neymiş görelim
        Console.WriteLine($"🎟️ [AUTH - TOKEN GENERATE] Platform: {platform} | Generated for IP: {cleanIp}");

        var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET");
        var expirationMin = double.Parse(Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES") ?? "10080");

        // 2. TEMİZ IP'YI CLAIM OLARAK EKLE
        var claims = new[]
        {
            new Claim("ip", cleanIp), // Artık temizlenmiş IP'yi mühürlüyoruz
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expirationDate = DateTime.UtcNow.AddMinutes(expirationMin);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expirationDate,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        if (platform.ToLower() == "web")
        {
            Response.Cookies.Append("jwt_token", tokenString, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = expirationDate,
                Path = "/"
            });

            return Ok(new { success = true, expiresAt = expirationDate });
        }

        return Ok(new { success = true, token = tokenString, expiresAt = expirationDate });
    }
}
