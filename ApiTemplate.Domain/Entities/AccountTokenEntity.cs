using ApiTemplate.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiTemplate.Domain.Entities
{
    [Table("AspNetUserTokens")]
    public class AccountTokenEntity : IdentityUserToken<int>
    {
        public AccountEntity User { get; private set; }

        public static AccountTokenEntity Create(
           int userId,
           string name,
           string loginProvider,
           string value)
        {
            return new AccountTokenEntity
            {
                UserId = userId,
                LoginProvider = loginProvider,
                Name = name,
                Value = value,
            };
        }

        public static async Task<AccountTokenEntity> Create(
            AccountEntity user,
            string name,
            string loginProvider,
            UserManager<AccountEntity> userManager)
        {
            return new AccountTokenEntity
            {
                UserId = user.Id,
                LoginProvider = loginProvider,
                Name = name,
                Value = await userManager.GenerateEmailConfirmationTokenAsync(user),
            };
        }

        public static void EnsureNoDuplicateTokensByNameAndProvider(List<AccountTokenEntity> tokens)
        {
            var isAllHaveSameType = tokens.Select(c => c.LoginProvider).Distinct().Count() == 1;
            if (!isAllHaveSameType)
                throw new DomainException("This code is invalid, please request a new one");

            var tokenNames = tokens.Select(c => c.Name).ToList();
            var hasNoDuplicatesByName = tokenNames.Distinct().Count() == tokenNames.Count;
            if (hasNoDuplicatesByName)
                throw new DomainException("This code is invalid, please request a new one");
        }
    }

    public enum TokenEnum
    {
        PasswordToken = 1,
        EmailToken,
        SignUpToken,
        UnsubscribeSMS
    }
}
