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
    }

    public enum RoleEnum
    {
        SuperAdmin = 1,
        Admin,
    }
}
