using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiTemplate.Domain.Entities
{
    [Table("AspNetUserTokens")]
    public class AccountTokenEntity : IdentityUserToken<int>
    {
        public AccountEntity User { get; set; }
    }

    public enum TokenEnum
    {
        PasswordToken = 1,
        EmailToken,
        SignUpToken,
        UnsubscribeSMS
    }
}
