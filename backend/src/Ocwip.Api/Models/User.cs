using System.ComponentModel.DataAnnotations;

namespace Ocwip.Api.Models
{
    public class User
    {
        public int Id { get; set; }

        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty; // Must be set to unique

        [Required]
        private string PasswordHash { get; set; } = string.Empty;

        public Role Role;

        [StringLength(11, MinimumLength = 11)]
        public string PESEL { get; set; } = string.Empty; // Must be encrypted

        public bool IsVerified { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int EntityId { get; set; } // 1:1 Relation, User <-> Entity
        public Entity Entity { get; set; } = null!;


    }
}
