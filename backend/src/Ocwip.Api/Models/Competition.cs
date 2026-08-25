using System.ComponentModel.DataAnnotations;

namespace Ocwip.Api.Models
{
    public class Competition
    {
        public Guid Id { get; set; }
        [Required]
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        public DateTimeOffset StartDate { get; set; } // UTC standard. Date right down to the minute.
        
        public DateTimeOffset EndDate { get; set; } // UTC standard. Date right down to the minute.

        [Range(0.01, double.MaxValue)]
        public decimal MaxGrantAmount { get; set; }
        public Status Status { get; set; }

        public ICollection<FormDefinition> FormDefinitions { get; set; } = [];

    }
}
