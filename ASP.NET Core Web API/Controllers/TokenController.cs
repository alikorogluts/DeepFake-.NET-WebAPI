using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
    public IActionResult GenerateToken([FromHeader(Name = "X-Client-Token")] string? clientToken)
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

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: creds
        );

        return Ok(new
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes)
        });
    }
}