using ApiTemplate.Domain.Entities;

namespace ApiTemplate.Application.Models
{
    public class CreateAccountDTO
    {
        public string Email { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public RoleEnum Role { get; set; }

        public string Password { get; set; }
    }
}
