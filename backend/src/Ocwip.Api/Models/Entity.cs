namespace Ocwip.Api.Models
{
    public class Entity
    {
        public int Id { get; set; }
        public EntityType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ContactInformation { get; set; } = string.Empty;

        public string? Nip { get; set; } //Sensitive Information

        public string? Address { get; set; } //Sensitive Information

        public User User { get; set; } = null!;
    }
}
