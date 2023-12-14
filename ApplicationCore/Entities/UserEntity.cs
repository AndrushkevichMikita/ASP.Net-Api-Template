﻿using Microsoft.AspNetCore.Identity;

namespace ApplicationCore.Entities
{
    public class UserEntity : IdentityUser<int>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public RoleEnum Role { get; set; }
        public bool CheckLocked() => LockoutEnabled && LockoutEnd?.UtcDateTime > DateTime.UtcNow;
    }

    public enum RoleEnum
    {
        SuperAdmin = 1,
        Admin,
    }
}
