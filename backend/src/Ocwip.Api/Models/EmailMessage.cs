namespace Ocwip.Api.Models
{
    public sealed record EmailMessage(
      string To,
      string Subject,
      string Body);
}
