using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CRMProjectAPI.Services
{
    public interface IJwtService
    {
        string GenerateToken(JwtUserClaims claims);
        ClaimsPrincipal? ValidateToken(string token);
    }

    public class JwtUserClaims
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public byte ISAdmin { get; set; }  // 0=User, 1=Admin, 2=SuperAdmin
        public int CompanyId { get; set; }
        public string? Picture { get; set; }
    }

    public class JwtService : IJwtService
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expiryHours;

        public JwtService(IConfiguration configuration)
        {
            _secretKey = configuration["JwtSettings:SecretKey"]
                ?? throw new InvalidOperationException("JwtSettings:SecretKey tanımlı değil");
            _issuer = configuration["JwtSettings:Issuer"]
                ?? throw new InvalidOperationException("JwtSettings:Issuer tanımlı değil");
            _audience = configuration["JwtSettings:Audience"]
                ?? throw new InvalidOperationException("JwtSettings:Audience tanımlı değil");
            _expiryHours = int.TryParse(configuration["JwtSettings:ExpiryHours"], out int h) ? h : 8;
        }

        public string GenerateToken(JwtUserClaims user)
        {
            SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            List<Claim> claims = new List<Claim>
{
    new Claim(JwtRegisteredClaimNames.Sub,   user.UserId.ToString()),
    new Claim(JwtRegisteredClaimNames.Email, user.Email),
    new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
    new Claim("userId",    user.UserId.ToString()),
    new Claim("username",  user.Username),
    new Claim("fullName",  user.FullName),
    new Claim("isAdmin",   user.ISAdmin.ToString()),
    new Claim("companyId", user.CompanyId.ToString()),
    new Claim("picture",   user.Picture ?? ""),
    new Claim(ClaimTypes.Role, user.ISAdmin == 2 ? "SuperAdmin"
                             : user.ISAdmin == 1 ? "Admin"
                             : "User")
};

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(_expiryHours),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
                TokenValidationParameters parameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _issuer,
                    ValidAudience = _audience,
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.Zero
                };
                return handler.ValidateToken(token, parameters, out _);
            }
            catch
            {
                return null;
            }
        }
    }
}