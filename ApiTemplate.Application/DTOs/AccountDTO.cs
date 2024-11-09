using ApiTemplate.Domain.Entities;

namespace ApiTemplate.Application.Models
{
    public class AccountDTO
    {
        public int Id { get; set; }

        public string Email { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public bool EmailConfirmed { get; set; }

        public bool PhoneNumberConfirmed { get; set; }

        public bool LockoutEnabled { get; set; }

        public bool AccessFailedCount { get; set; }

        public DateTimeOffset? LockoutEnd { get; set; }

        public RoleEnum Role { get; set; }
    }
}
