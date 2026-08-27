

namespace Ocwip.Api.Models
{
    public class Competition
    {
        public Guid Id { get; set; }
        
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime ClosedAt { get; set; }

        private DateTimeOffset _startDate;
        public DateTimeOffset StartDate
        {
            get => _startDate;
            set => _startDate = value.ToUniversalTime();
        }

        private DateTimeOffset _endDate;
        public DateTimeOffset EndDate
        {
            get => _endDate;
            set => _endDate = value.ToUniversalTime();
        }

        public decimal MaxGrantAmount { get; set; }
        public CompetitionStatus Status { get; set ; }

        public ICollection<FormDefinition> FormDefinitions { get; set; } = [];

    }
}
