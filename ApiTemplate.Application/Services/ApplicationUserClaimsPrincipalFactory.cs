using ApiTemplate.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ApiTemplate.Application.Services
{
    public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AccountEntity, IdentityRole<int>>
    {
        public ApplicationUserClaimsPrincipalFactory(
            UserManager<AccountEntity> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, roleManager, optionsAccessor) { }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AccountEntity user)
          => await GenerateAdjustedClaimsAsync(user);

        public async Task<ClaimsIdentity> GenerateAdjustedClaimsAsync(AccountEntity user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            if (user.LockoutEnabled && user.LockoutEnd.HasValue)
                identity.AddClaim(new Claim(ClaimTypes.AuthorizationDecision, user.LockoutEnd.Value.UtcDateTime.ToString()));

            return identity;
        }
    }
}
