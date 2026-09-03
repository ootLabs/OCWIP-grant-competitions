using Microsoft.AspNetCore.Identity;


namespace Ocwip.Api.Models
{
    public class User : IdentityUser<Guid>
    {
        //Id, Email and PasswordHash are already in IdentityUser
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;

        public Role Role { get; set; }

        
        public string Pesel { get; set; } = string.Empty; // Must be encrypted

        public bool IsVerified { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset DeactivatedAt { get; set; }
        public Guid? EntityId { get; set; } // 1:1 Relation, User <-> Entity
        public Entity? Entity { get; set; } = null!;


    }
}
