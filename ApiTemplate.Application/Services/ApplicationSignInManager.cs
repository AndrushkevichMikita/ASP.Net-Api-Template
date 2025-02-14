using ApiTemplate.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ApiTemplate.Application.Services
{
    public class ApplicationSignInManager : SignInManager<Account>
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<Account> _userManager;
        private readonly ApplicationUserClaimsPrincipalFactory _applicationUserClaimsPrincipalFactory;

        public ApplicationSignInManager(
            UserManager<Account> userManager,
            IHttpContextAccessor contextAccessor,
            IUserClaimsPrincipalFactory<Account> claimsFactory,
            IConfiguration configuration,
            IOptions<IdentityOptions> optionsAccessor,
            ILogger<SignInManager<Account>> logger,
            IAuthenticationSchemeProvider schemes,
            IUserConfirmation<Account> confirmation,
            ApplicationUserClaimsPrincipalFactory applicationUserClaimsPrincipalFactory)
            : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
        {
            _userManager = userManager;
            _configuration = configuration;
            _applicationUserClaimsPrincipalFactory = applicationUserClaimsPrincipalFactory;
        }

        public static TokenValidationParameters GetTokenValidationParameters(IConfiguration configuration)
          => new()
          {
              ValidateIssuer = true,
              ValidateAudience = true,
              ValidateLifetime = true,
              ValidateIssuerSigningKey = true,
              ValidIssuer = configuration["Jwt:Issuer"],
              ValidAudience = configuration["Jwt:Audience"],
              IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(configuration["Jwt:Key"]))
          };

        public static JwtSecurityToken CreateJWTToken(IConfiguration configuration, IEnumerable<Claim> claims)
          => new(issuer: configuration["Jwt:Issuer"],
                 audience: configuration["Jwt:Audience"],
                 claims: claims,
                 expires: DateTime.Now.AddMinutes(int.Parse(configuration["Jwt:LifetimeMinutes"])),
                 signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"])), SecurityAlgorithms.HmacSha256));

        public virtual async Task<string> GenerateJwtTokenAsync(Account user)
        {
            var claims = await _applicationUserClaimsPrincipalFactory.GenerateAdjustedClaimsAsync(user);
            var token = CreateJWTToken(_configuration, claims.Claims);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public virtual async Task<string> GenerateRefreshTokenAsync(Account user)
        {
            var refreshToken = Guid.NewGuid().ToString();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.Now.AddDays(int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"]));
            await _userManager.UpdateAsync(user);

            return refreshToken;
        }
    }
}
