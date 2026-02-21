using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ASP.NET_Core_Web_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenController : ControllerBase
{
    private readonly IConfiguration _config;

    public TokenController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public IActionResult GenerateToken(
        [FromHeader(Name = "X-Client-Token")] string? clientToken,
        [FromHeader(Name = "X-Client-Platform")] string platform = "web") // YENİ: Platform parametresi
    {
        // 1. İstemci (App) Doğrulaması
        var expectedToken = _config["AppConfig:ClientToken"];
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

        // 4. Token'ı Şifrele ve Üret
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSettings:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expirationMinutes = Convert.ToDouble(_config["JwtSettings:ExpirationInMinutes"]);
        var expirationDate = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expirationDate,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        // 5. YENİ: Platforma Göre Güvenlik Dağıtımı (Web vs Mobile)
        if (platform.ToLower() == "web")
        {
            // Web (Next.js) için kırılamaz HttpOnly çerez oluştur
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true, // JavaScript KESİNLİKLE okuyamaz (XSS koruması)
                Secure = true,   // Sadece HTTPS üzerinden gider (Ağa sızanlar göremez)
                SameSite = SameSiteMode.Strict, // CSRF koruması
                Expires = expirationDate // Çerez ömrü = Token ömrü
            };
            
            Response.Cookies.Append("jwt_token", tokenString, cookieOptions);
            
            // Web'e Token dönmüyoruz! Sadece başarılı olduğuna dair mesaj dönüyoruz.
            return Ok(new 
            { 
                success = true, 
                message = "Güvenli giriş yapıldı, token HttpOnly Cookie kasasına yazıldı.",
                expiresAt = expirationDate
            });
        }
        
        // Eğer platform "mobile" ise klasik JSON yanıtını dön
        return Ok(new
        {
            success = true,
            token = tokenString,
            expiresAt = expirationDate
        });
    }
}