using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The competition parameters of wizard steps 1.2 to 1.6 over HTTP (T-20a):
/// what is stored, what a guest gets to see, and which combinations are
/// refused before they can reach a check constraint as a 500.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompetitionParametersTests
    : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public CompetitionParametersTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private (WebApplicationFactory<Program> Host, FixedTimeProvider Clock) Host() =>
        CompetitionTestHost.Create(_factory, _database);

    /// <summary>
    /// A date far enough past the closing of the intake to satisfy the five
    /// year retention floor, so a test about something else does not trip over
    /// it.
    /// </summary>
    private static readonly DateOnly PersonalDataUntil = new(2032, 1, 1);

    private static CompetitionRequest FullRequest(
        IReadOnlyList<Guid>? contacts = null) =>
        CompetitionTestHost.Request() with
        {
            ExpectedResults = "Dziesięć wspartych inicjatyw lokalnych.",
            RulesUrl = "https://ocwip.pl/regulamin",
            SubmissionNotice = "Dziękujemy, wniosek trafił do nas.",
            SubmissionEmailBody = "Potwierdzamy przyjęcie wniosku.",
            RequiresPaperSubmission = true,
            PaperSubmissionDeadline =
                new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero),
            PaperSubmissionAddress = "ul. Damrota 4, 45-064 Opole",
            ProjectStartDate = new DateOnly(2026, 11, 1),
            ProjectEndDate = new DateOnly(2027, 6, 30),
            TotalPoolAmount = 120000m,
            MinGrantAmount = 1000m,
            MaxIndirectCostPercent = 10m,
            MaxInstitutionalDevelopmentPercent = 50m,
            PercentageBasis = PercentageBasis.TotalProjectValue,
            MaxAverageAnnualRevenue = 200000m,
            PersonalDataProcessedUntil = PersonalDataUntil,
            CostCategories = [CostCategory.DirectCosts, CostCategory.IndirectCosts],
            Attachments =
            [
                new CompetitionAttachmentRequest(
                    "Odpis z rejestru",
                    "Jeśli nie jest ogólnodostępny.",
                    AttachmentRequirement.RequiredOutsideKrs,
                    [AllowedFileFormat.Pdf, AllowedFileFormat.Jpg]),
                new CompetitionAttachmentRequest(
                    "Sprawozdanie finansowe",
                    null,
                    AttachmentRequirement.Required,
                    [AllowedFileFormat.Pdf]),
            ],
            ContactUserIds = contacts,
        };

    [RequiresDatabaseFact]
    public async Task Every_parameter_of_steps_1_2_to_1_6_survives_the_round_trip()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var staff = await SessionTestHost.CreateAccountAsync(
            host, SessionTestHost.Email("kontakt"), Role.Operator);

        // Act
        var created = await CompetitionTestHost.CreateAsync(
            client, FullRequest([staff.Id]));

        var read = await client.GetFromJsonAsync<CompetitionResponse>(
            $"/competitions/{created.Id}");

        // Assert
        Assert.NotNull(read);
        Assert.Equal("Dziesięć wspartych inicjatyw lokalnych.", read.ExpectedResults);
        Assert.Equal("https://ocwip.pl/regulamin", read.RulesUrl);
        Assert.Equal("Dziękujemy, wniosek trafił do nas.", read.SubmissionNotice);
        Assert.Equal("Potwierdzamy przyjęcie wniosku.", read.SubmissionEmailBody);
        Assert.True(read.RequiresPaperSubmission);
        Assert.Equal("ul. Damrota 4, 45-064 Opole", read.PaperSubmissionAddress);
        Assert.Equal(new DateOnly(2026, 11, 1), read.ProjectStartDate);
        Assert.Equal(new DateOnly(2027, 6, 30), read.ProjectEndDate);
        Assert.Equal(120000m, read.TotalPoolAmount);
        Assert.Equal(1000m, read.MinGrantAmount);
        Assert.Equal(10m, read.MaxIndirectCostPercent);
        Assert.Equal(50m, read.MaxInstitutionalDevelopmentPercent);
        Assert.Equal(PercentageBasis.TotalProjectValue, read.PercentageBasis);
        Assert.Equal(200000m, read.MaxAverageAnnualRevenue);
        Assert.Equal(PersonalDataUntil, read.PersonalDataProcessedUntil);

        // The order the operator arranged them in, not the order the rows came
        // back from the database.
        Assert.Equal(
            [CostCategory.DirectCosts, CostCategory.IndirectCosts],
            read.CostCategories);
        Assert.Equal(
            ["Odpis z rejestru", "Sprawozdanie finansowe"],
            read.Attachments.Select(attachment => attachment.Title));
        Assert.Equal(
            AttachmentRequirement.RequiredOutsideKrs,
            read.Attachments[0].Requirement);
        Assert.Equal(
            [AllowedFileFormat.Pdf, AllowedFileFormat.Jpg],
            read.Attachments[0].AllowedFormats);

        var contact = Assert.Single(read.Contacts);
        Assert.Equal(staff.Id, contact.UserId);
        Assert.Equal(staff.Email, contact.Email);

        // The defaults hold when nothing overrides them, so T-32 has limits to
        // enforce without the operator having typed any.
        Assert.Equal(Competition.DefaultMaxAttachmentSizeInBytes,
            read.MaxAttachmentSizeInBytes);
        Assert.Equal(Competition.DefaultMaxApplicationSizeInBytes,
            read.MaxApplicationSizeInBytes);
    }

    [RequiresDatabaseFact]
    public async Task A_paper_deadline_is_stored_in_utc_and_cut_to_a_whole_minute()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        // 15:00:45 in Poland during summer time, which is 13:00 UTC once the
        // seconds are gone. Same rule as the intake window, because it is the
        // same kind of deadline.
        var request = FullRequest() with
        {
            PaperSubmissionDeadline =
                new DateTimeOffset(2026, 10, 7, 15, 0, 45, TimeSpan.FromHours(2)),
        };

        // Act
        var created = await CompetitionTestHost.CreateAsync(client, request);

        // Assert
        await using var context = _database.CreateContext();
        var stored = await context.Competitions
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        Assert.Equal(TimeSpan.Zero, stored.PaperSubmissionDeadline!.Value.Offset);
        Assert.Equal(13, stored.PaperSubmissionDeadline.Value.Hour);
        Assert.Equal(0, stored.PaperSubmissionDeadline.Value.Second);
    }

    [RequiresDatabaseFact]
    public async Task A_competition_that_picks_no_cost_categories_gets_the_three_defaults()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        // Act
        var created = await CompetitionTestHost.CreateAsync(
            client, CompetitionTestHost.Request());

        // Assert
        Assert.Equal(
            [
                CostCategory.DirectCosts,
                CostCategory.InstitutionalDevelopment,
                CostCategory.IndirectCosts,
            ],
            created.CostCategories);
    }

    [RequiresDatabaseFact]
    public async Task Editing_replaces_the_lists_instead_of_adding_to_them()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var staff = await SessionTestHost.CreateAccountAsync(
            host, SessionTestHost.Email("kontakt"), Role.Operator);

        var request = FullRequest([staff.Id]);
        var created = await CompetitionTestHost.CreateAsync(client, request);

        var edited = request with
        {
            CostCategories = [CostCategory.DirectCosts],
            Attachments =
            [
                new CompetitionAttachmentRequest(
                    "CIT za 2025",
                    null,
                    AttachmentRequirement.Required,
                    [AllowedFileFormat.Pdf]),
            ],
            ContactUserIds = [],
        };

        // Act
        var response = await client.PutAsJsonAsync(
            $"/competitions/{created.Id}", edited);

        // Assert
        response.EnsureSuccessStatusCode();

        var read = (await response.Content
            .ReadFromJsonAsync<CompetitionResponse>())!;

        // One of each, not three plus one: the wizard edits these as lists, so
        // a save that appended would double them on every trip through it.
        Assert.Equal([CostCategory.DirectCosts], read.CostCategories);
        Assert.Equal("CIT za 2025", Assert.Single(read.Attachments).Title);
        Assert.Empty(read.Contacts);

        // And the rows are gone from the database, not just from the answer.
        await using var context = _database.CreateContext();
        Assert.Equal(
            1,
            await context.CompetitionAttachments
                .CountAsync(x => x.CompetitionId == created.Id));
        Assert.Equal(
            0,
            await context.CompetitionContacts
                .CountAsync(x => x.CompetitionId == created.Id));
    }

    [RequiresDatabaseFact]
    public async Task A_guest_sees_what_to_prepare_but_not_the_messages_sent_after_a_submission()
    {
        // Arrange
        var (host, clock) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var staff = await SessionTestHost.CreateAccountAsync(
            host, SessionTestHost.Email("kontakt"), Role.Operator);

        var created = await CompetitionTestHost.CreateAsync(
            client, FullRequest([staff.Id]));

        (await CompetitionTestHost.ChangeStatusAsync(
            client, created.Id, CompetitionStatus.Published))
            .EnsureSuccessStatusCode();

        clock.Now = CompetitionTestHost.Start.AddMinutes(1);

        // Act, with no account at all.
        var guest = host.CreateClient();
        var response = await guest.GetAsync($"/public/competitions/{created.Id}");

        // Assert
        response.EnsureSuccessStatusCode();

        var read = (await response.Content
            .ReadFromJsonAsync<PublicCompetitionResponse>())!;

        Assert.Equal("https://ocwip.pl/regulamin", read.RulesUrl);
        Assert.True(read.RequiresPaperSubmission);
        Assert.Equal("ul. Damrota 4, 45-064 Opole", read.PaperSubmissionAddress);
        Assert.Equal(1000m, read.MinGrantAmount);
        Assert.Equal(2, read.Attachments.Count);
        Assert.Equal(staff.Id, Assert.Single(read.Contacts).UserId);

        // The two messages after a submission are addressed to somebody who
        // already applied, so the public record cannot carry them: this is a
        // check on the TYPE, which is the half that cannot rot.
        Assert.DoesNotContain(
            "submissionNotice",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [RequiresDatabaseTheory]
    [InlineData(true, false, "paperSubmissionAddress")]
    [InlineData(false, true, "paperSubmissionDeadline")]
    public async Task The_paper_switch_and_its_fields_have_to_agree(
        bool requiresPaper,
        bool carriesDeadline,
        string field)
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var request = CompetitionTestHost.Request() with
        {
            RequiresPaperSubmission = requiresPaper,
            PaperSubmissionDeadline = carriesDeadline || requiresPaper
                ? new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero)
                : null,
            PaperSubmissionAddress = null,
        };

        // Act
        var response = await client.PostAsJsonAsync("/competitions", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, await Problems(response));
    }

    [RequiresDatabaseFact]
    public async Task Personal_data_may_not_be_erased_before_five_years_after_the_intake_closes()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        // The intake closes on 2026-09-30, so 2031-09-29 is one day short.
        var request = CompetitionTestHost.Request() with
        {
            PersonalDataProcessedUntil = new DateOnly(2031, 9, 29),
        };

        // Act
        var response = await client.PostAsJsonAsync("/competitions", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problems = await Problems(response);
        Assert.Contains("personalDataProcessedUntil", problems);

        // D12: the message carries the date it was decided by, not the rule.
        Assert.Contains("30.09.2031", problems);

        // And the floor itself passes.
        var accepted = request with
        {
            PersonalDataProcessedUntil = new DateOnly(2031, 9, 30),
        };

        (await client.PostAsJsonAsync("/competitions", accepted))
            .EnsureSuccessStatusCode();
    }

    [RequiresDatabaseFact]
    public async Task A_continuous_intake_counts_the_five_years_from_the_opening()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        // There is no closing moment to count from, and refusing every date
        // would make the field impossible to fill in, so the floor is five
        // years from the opening (2026-09-01). See docs/architektura.md.
        var request = CompetitionTestHost.Request(continuous: true) with
        {
            PersonalDataProcessedUntil = new DateOnly(2031, 8, 31),
        };

        // Act
        var response = await client.PostAsJsonAsync("/competitions", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("personalDataProcessedUntil", await Problems(response));

        (await client.PostAsJsonAsync(
            "/competitions",
            CompetitionTestHost.Request(continuous: true) with
            {
                PersonalDataProcessedUntil = new DateOnly(2031, 9, 1),
            }))
            .EnsureSuccessStatusCode();
    }

    [RequiresDatabaseTheory]
    // The money and the percentages, each one the value a check constraint
    // would otherwise answer with a 500.
    [InlineData("minGrantAmount", 6000, null, null)]
    [InlineData("totalPoolAmount", null, 0, null)]
    [InlineData("maxIndirectCostPercent", null, null, 101)]
    public async Task Limits_outside_their_range_come_back_naming_the_field(
        string field,
        int? minimumGrant,
        int? pool,
        int? percent)
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        // MaxGrantAmount is 5000 in the shared request, so 6000 as a minimum is
        // a competition whose smallest grant is larger than its largest.
        var request = CompetitionTestHost.Request() with
        {
            MinGrantAmount = minimumGrant,
            TotalPoolAmount = pool,
            MaxIndirectCostPercent = percent,
        };

        // Act
        var response = await client.PostAsJsonAsync("/competitions", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, await Problems(response));
    }

    [RequiresDatabaseFact]
    public async Task A_rules_link_has_to_be_an_absolute_http_address()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        // Resolved against our own address by a browser, so the applicant ends
        // up on a page of ours that does not exist. The javascript: variant is
        // the same mistake with teeth.
        var request = CompetitionTestHost.Request() with
        {
            RulesUrl = "www.ocwip.pl/regulamin",
        };

        // Act
        var response = await client.PostAsJsonAsync("/competitions", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("rulesUrl", await Problems(response));
    }

    [RequiresDatabaseFact]
    public async Task An_attachment_nobody_could_hand_in_is_refused()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var request = CompetitionTestHost.Request() with
        {
            Attachments =
            [
                new CompetitionAttachmentRequest(
                    "Odpis z rejestru",
                    null,
                    AttachmentRequirement.Required,
                    [AllowedFileFormat.Pdf]),
                new CompetitionAttachmentRequest(
                    "  ",
                    null,
                    AttachmentRequirement.Required,
                    []),
            ],
        };

        // Act
        var response = await client.PostAsJsonAsync("/competitions", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problems = await Problems(response);

        // Indexed, because a list of attachments that answers "tytuł jest
        // wymagany" without saying which one sends the operator hunting.
        Assert.Contains("attachments[1].title", problems);
        Assert.Contains("attachments[1].allowedFormats", problems);
        Assert.DoesNotContain("attachments[0]", problems);
    }

    [RequiresDatabaseFact]
    public async Task Only_an_active_staff_account_may_be_a_contact_person()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        // An applicant's address published on the competition page as the
        // person to ask would be a leak of our own making.
        var applicant = await SessionTestHost.CreateAccountAsync(
            host, SessionTestHost.Email("wnioskodawca"), Role.Applicant);

        // Act
        var response = await client.PostAsJsonAsync(
            "/competitions",
            CompetitionTestHost.Request() with { ContactUserIds = [applicant.Id] });

        var unknown = await client.PostAsJsonAsync(
            "/competitions",
            CompetitionTestHost.Request() with { ContactUserIds = [Guid.NewGuid()] });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, unknown.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task The_same_contact_person_cannot_be_listed_twice()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var staff = await SessionTestHost.CreateAccountAsync(
            host, SessionTestHost.Email("kontakt"), Role.Operator);

        // Act
        var response = await client.PostAsJsonAsync(
            "/competitions",
            CompetitionTestHost.Request() with
            {
                ContactUserIds = [staff.Id, staff.Id],
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("contactUserIds", await Problems(response));
    }

    private static Task<string> Problems(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync();
}
