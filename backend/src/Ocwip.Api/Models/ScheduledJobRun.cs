namespace Ocwip.Api.Models
{
    /// <summary>
    /// One run of a background job for one object at one due moment (T-105):
    /// "the reminder for application X, due 3 days before the intake closing
    /// at Y". The unique key (job, subject, due) is what makes a job
    /// idempotent: however many times the scheduler looks, the row for that
    /// key is inserted once and completed once.
    ///
    /// At most once, not at least once. A run is claimed before its side
    /// effect (a mail) and completed after it. A run that was claimed and
    /// never completed means the process died in between, and it is left as
    /// it is: the mail may have gone, and a second one is worse than none. A
    /// failure the job itself saw (the relay refused) releases the claim with
    /// the error, because then nothing went out.
    /// </summary>
    public class ScheduledJobRun
    {
        public Guid Id { get; set; }

        /// <summary>The job's name, for example "intake-reminder".</summary>
        public string Job { get; set; } = string.Empty;

        /// <summary>What the run is about: an application, a contract.</summary>
        public Guid SubjectId { get; set; }

        /// <summary>The moment the run became due, in UTC; part of the key, so a new deadline is a new run.</summary>
        public DateTimeOffset DueAt { get; set; }

        public DateTimeOffset? ClaimedAt { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public int Attempts { get; set; }

        /// <summary>The type of the last failure only: a message may carry an address.</summary>
        public string? LastError { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
