using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Deepfake.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost]
    public IActionResult GenerateToken(
        [FromHeader(Name = "X-Client-Token")] string? clientToken,
        [FromHeader(Name = "X-Client-Platform")] string platform = "web") 
    {
        // 1. İstemci (App) Doğrulaması (Farklı isimlendirmelere karşı korumalı)
        var expectedToken = _config["AppConfig:ClientToken"] ?? _config["AppConfig__ClientToken"];
        
        if (string.IsNullOrEmpty(expectedToken))
        {
            return StatusCode(500, new { message = "Sunucu Hatası: ClientToken ortam değişkeni bulunamadı!" });
        }

        if (clientToken != expectedToken)
        {
            return Unauthorized(new { message = "Geçersiz istemci uygulaması." });
        }

        // 2. IP Adresini Al
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // 3. JWT İçine IP'yi Göm (Claim olarak)
        var claims = new[]
        {
            new Claim("ip", ipAddress),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // 4. Şifreyi ve Süreyi Güvenli Bir Şekilde Al (Çökmeyi Önleyen Kısım)
        var secretKey = _config["JWT_SECRET"] ?? _config["JwtSettings:Secret"] ?? _config["JwtSettings__Secret"];
        
        if (string.IsNullOrEmpty(secretKey))
        {
            return StatusCode(500, new { message = "Sunucu Hatası: JWT Secret ortam değişkeni bulunamadı!" });
        }

        var expirationStr = _config["JwtSettings:ExpirationInMinutes"] ?? _config["JwtSettings__ExpirationInMinutes"] ?? "60";
        if (!double.TryParse(expirationStr, out double expirationMinutes))
        {
            expirationMinutes = 60; // Okuyamazsa varsayılan 1 saat
        }

        // 5. Token'ı Şifrele ve Üret
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expirationDate = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expirationDate,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        // 6. Platforma Göre Güvenlik Dağıtımı (Web vs Mobile)
        if (platform.ToLower() == "web")
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true, 
                Secure = true,   
                //SameSite = SameSiteMode.Strict,  ToDo yayınlarken burayı düzelt 
                SameSite = SameSiteMode.None,
                Expires = expirationDate ,
                Path = "/"
            };
            
            Response.Cookies.Append("jwt_token", tokenString, cookieOptions);
            
            return Ok(new 
            { 
                success = true, 
                message = "Güvenli giriş yapıldı, token HttpOnly Cookie kasasına yazıldı.",
                expiresAt = expirationDate
            });
        }
        
        return Ok(new
        {
            success = true,
            token = tokenString,
            expiresAt = expirationDate
        });
    }
}