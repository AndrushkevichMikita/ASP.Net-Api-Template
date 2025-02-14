using ApiTemplate.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ApiTemplate.Domain.Entities
{
    public class Account : IdentityUser<int>
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

        public async Task CreateAsync(
            UserManager<Account> userManager,
            string password)
        {
            var createResult = await userManager.CreateAsync(this, password);
            if (!createResult.Succeeded)
                throw new DomainException(string.Join(" ", createResult.Errors.Select(c => c.Description)));
        }

        public async Task AssignRoleAsync(
            UserManager<Account> userManager,
            RoleEnum role)
        {
            var addedToRoleResult = await userManager.AddToRoleAsync(this, role.ToString());
            if (!addedToRoleResult.Succeeded)
                throw new DomainException(string.Join(" ", addedToRoleResult.Errors.Select(c => c.Description)));
        }

        public async Task DeleteAsync(UserManager<Account> userManager)
        {
            var createResult = await userManager.DeleteAsync(this);
            if (!createResult.Succeeded)
                throw new DomainException(string.Join(" ", createResult.Errors.Select(c => c.Description)));
        }

        public async Task EnsureEmailConfirmedAsync(UserManager<Account> userManager)
        {
            var isConfirmed = await userManager.IsEmailConfirmedAsync(this);
            if (!isConfirmed)
                throw new DomainException("The email address is unconfirmed");
        }

        public async Task EnsureConfirmEmailAsync(
            UserManager<Account> userManager,
            AccountTokenEntity accountTokenEntity)
        {
            var confirmationResult = await userManager.ConfirmEmailAsync(this, accountTokenEntity.Value);
            if (!confirmationResult.Succeeded)
                throw new DomainException(confirmationResult.Errors.FirstOrDefault()!.Description);
        }

        public async Task EnsurePasswordAsync(
            UserManager<Account> userManager,
            string password)
        {
            var isPasswordCorrect = await userManager.CheckPasswordAsync(this, password);
            if (!isPasswordCorrect)
                throw new DomainException("The specified password invalid");
        }

        public void ValidateRefreshToken(string refreshToken)
        {
            if (this == null || RefreshToken != refreshToken || RefreshTokenExpiryTime <= DateTime.UtcNow)
                throw new DomainException("Refresh token invalid");
        }
    }

    public enum RoleEnum
    {
        SuperAdmin = 1,
        Admin,
    }
}
