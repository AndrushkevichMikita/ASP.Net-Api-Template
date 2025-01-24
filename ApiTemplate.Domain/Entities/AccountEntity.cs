using ApiTemplate.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ApiTemplate.Domain.Entities
{
    public class AccountEntity : IdentityUser<int>
    {
        [MaxLength(50)]
        public string? RefreshToken { get; set; }

        public DateTime Created { get; set; }

        public DateTime LastUpdated { get; set; }

        public DateTime? RefreshTokenExpiryTime { get; set; }

        [MaxLength(250)]
        public string FirstName { get; set; }

        [MaxLength(250)]
        public string LastName { get; set; }

        public RoleEnum Role { get; set; }

        public ICollection<AccountTokenEntity> Tokens { get; set; }

        public bool IsLocked() => LockoutEnabled && LockoutEnd?.UtcDateTime > DateTime.UtcNow;

        public async Task ConfirmEmailAsync(UserManager<AccountEntity> userManager, AccountTokenEntity accountTokenEntity)
        {
            var res = await userManager.ConfirmEmailAsync(this, accountTokenEntity.Value);
            if (!res.Succeeded)
                throw new DomainException(res.Errors.FirstOrDefault()!.Description);
        }

        public async Task<bool> IsEmailConfirmedAsync(UserManager<AccountEntity> userManager)
        {
            return await userManager.IsEmailConfirmedAsync(this);
        }

        public static void ValidateRefreshToken(AccountEntity account, string refreshToken)
        {
            if (account == null || account.RefreshToken != refreshToken || account.RefreshTokenExpiryTime <= DateTime.UtcNow)
                throw new DomainException("Refresh token invalid");
        }
    }

    public enum RoleEnum
    {
        SuperAdmin = 1,
        Admin,
    }
}
