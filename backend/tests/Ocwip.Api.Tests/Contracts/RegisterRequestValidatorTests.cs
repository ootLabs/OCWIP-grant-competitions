using Ocwip.Api.Contracts;
using Xunit;

namespace Ocwip.Api.Tests.Contracts;

/// <summary>
/// The address rule, written down here because T-12.0 switched Identity's
/// AllowedUserNameCharacters off: that filter would otherwise decide which
/// addresses may register, in English, as a side effect of a username concept
/// this product does not have.
/// </summary>
public sealed class RegisterRequestValidatorTests
{
    private const string ValidPassword = "Tajne-Haslo1";

    private static RegisterRequest Request(
        string email = "adam@example.org",
        string password = ValidPassword,
        string firstName = "Adam",
        string lastName = "Testowy") =>
        new(email, password, firstName, lastName);

    [Fact]
    public void A_complete_request_has_no_problems()
    {
        Assert.Empty(RegisterRequestValidator.Validate(Request()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nie-jest-adresem")]
    [InlineData("@example.org")]
    [InlineData("adam@")]
    [InlineData("adam @example.org")]
    // Parsed happily by MailAddress, which exposes the address inside it, so
    // without the round trip check this would register an account under an
    // address the person never typed.
    [InlineData("Adam Testowy <adam@example.org>")]
    // Parsed and round tripped by MailAddress, and refused by the
    // EmailAddressAttribute that Identity's UserValidator runs on its own.
    // Without the same check here, this address passes the edge and comes back
    // as Identity's English InvalidEmail against the password field.
    [InlineData("\"a@b\"@example.org")]
    // Invisible to a person and a different string to the unique index, so the
    // visible address would hold two accounts and one of them could never
    // receive its mail. MailAddress parses these and round trips them, and
    // EmailAddressAttribute counts one at sign, so nothing else here sees them.
    // Written as escapes rather than pasted: an invisible character in a test
    // case is a case nobody can read, and the next person deletes it as a typo.
    [InlineData("adam\u200b@example.org")]
    [InlineData("adam@exam\u00adple.org")]
    public void A_malformed_address_is_refused(string email)
    {
        var problems = RegisterRequestValidator.Validate(Request(email: email));

        Assert.True(problems.ContainsKey("email"));
    }

    [Theory]
    [InlineData("adam@example.org")]
    [InlineData("adam.testowy+konkurs@sub.example.org")]
    // Legal, and refused by Identity's default username filter, which is the
    // whole reason that filter is off and this rule lives here instead.
    [InlineData("o'brien@example.org")]
    [InlineData("zażółć@example.org")]
    public void A_legal_address_is_accepted(string email)
    {
        var problems = RegisterRequestValidator.Validate(Request(email: email));

        Assert.False(problems.ContainsKey("email"));
    }

    [Theory]
    [InlineData(" adam@example.org")]
    [InlineData("adam@example.org ")]
    [InlineData("\tadam@example.org\n")]
    public void Whitespace_around_the_address_is_trimmed_not_refused(string email)
    {
        // A pasted address arrives with the clipboard's whitespace on it. Left
        // in place it is refused as malformed, which is a message claiming the
        // address is wrong when it is not, and stored as written it would give
        // one address two accounts: the normalized copy keeps the space, so the
        // unique index sees two different values.
        var trimmed = RegisterRequestValidator.Trim(Request(email: email));

        Assert.Equal("adam@example.org", trimmed.Email);
        Assert.Empty(RegisterRequestValidator.Validate(trimmed));
    }

    [Fact]
    public void Trimming_leaves_the_password_alone()
    {
        // A space at either end of a password is a character of the password.
        var trimmed = RegisterRequestValidator.Trim(
            Request(password: " Tajne-Haslo1 ", firstName: " Adam ", lastName: " Testowy "));

        Assert.Equal(" Tajne-Haslo1 ", trimmed.Password);
        Assert.Equal("Adam", trimmed.FirstName);
        Assert.Equal("Testowy", trimmed.LastName);
    }

    [Fact]
    public void A_null_field_survives_trimming()
    {
        // The declared type says never null and JSON says otherwise: a body
        // with "email": null deserializes straight through the annotation.
        var trimmed = RegisterRequestValidator.Trim(
            new RegisterRequest(null!, null!, null!, null!));

        Assert.Equal(string.Empty, trimmed.Email);
        Assert.Equal(4, RegisterRequestValidator.Validate(trimmed).Count);
    }

    [Fact]
    public void The_password_never_reaches_the_generated_text_of_the_request()
    {
        // AGENTS.md security rule 4. The generated ToString of a record prints
        // every property, so the default one hands a credential to the first
        // log message or exception that interpolates the object.
        var text = Request(password: "Tajne-Haslo1").ToString();

        Assert.DoesNotContain("Tajne-Haslo1", text);
    }

    [Fact]
    public void An_address_longer_than_the_column_is_refused()
    {
        // 255 characters, one past the column width in UserConfiguration.cs.
        var local = new string('a', 255 - "@example.org".Length);

        var problems = RegisterRequestValidator.Validate(
            Request(email: $"{local}@example.org"));

        Assert.True(problems.ContainsKey("email"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_password_is_refused(string password)
    {
        var problems = RegisterRequestValidator.Validate(
            Request(password: password));

        Assert.True(problems.ContainsKey("password"));
    }

    [Fact]
    public void The_password_policy_is_not_repeated_here()
    {
        // Only presence. What makes a password acceptable is one decision and it
        // lives in Configuration/IdentityConfiguration.cs, so a short password
        // has to pass this validator and be refused there instead.
        var problems = RegisterRequestValidator.Validate(Request(password: "x"));

        Assert.False(problems.ContainsKey("password"));
    }

    [Theory]
    [InlineData("", "Adam", "firstName")]
    [InlineData("   ", "Adam", "firstName")]
    [InlineData("Adam", "", "lastName")]
    [InlineData("Adam", "   ", "lastName")]
    public void A_missing_name_is_refused(
        string firstName,
        string lastName,
        string expectedKey)
    {
        var problems = RegisterRequestValidator.Validate(
            Request(firstName: firstName, lastName: lastName));

        Assert.True(problems.ContainsKey(expectedKey));
    }

    [Theory]
    // A line break INSIDE a name, which Trim never reaches, stored exactly as
    // written. Nothing reads it yet and that is the reason it has to go now:
    // the verification mail (T-12.2) is the first thing that will, and there a
    // line break in a name is an extra mail header.
    [InlineData("Adam\r\nBcc: zly@example.org")]
    [InlineData("Adam\u0000")]
    // A format character rather than a control one, and just as unwelcome in
    // a column the schema treats as one line of text.
    [InlineData("Adam\u200b")]
    public void A_name_with_an_invisible_character_is_refused(string name)
    {
        Assert.True(RegisterRequestValidator
            .Validate(Request(firstName: name))
            .ContainsKey("firstName"));

        Assert.True(RegisterRequestValidator
            .Validate(Request(lastName: name))
            .ContainsKey("lastName"));
    }

    [Theory]
    [InlineData("firstName")]
    [InlineData("lastName")]
    public void A_name_longer_than_the_column_is_refused(string key)
    {
        var tooLong = new string('a', 101);

        var problems = RegisterRequestValidator.Validate(
            key == "firstName"
                ? Request(firstName: tooLong)
                : Request(lastName: tooLong));

        Assert.True(problems.ContainsKey(key));
    }

    [Fact]
    public void Every_field_reports_in_polish()
    {
        var problems = RegisterRequestValidator.Validate(
            new RegisterRequest(
                string.Empty, string.Empty, string.Empty, string.Empty));

        // docs/konwencje.md: UI text is Polish. Pinned as exact strings rather
        // than sniffed for diacritics, because half of these words have none
        // and such a heuristic would pass English and fail Polish by turns.
        Assert.Equal(4, problems.Count);
        Assert.Equal(
            "Adres e-mail jest wymagany.", Assert.Single(problems["email"]));
        Assert.Equal(
            "Hasło jest wymagane.", Assert.Single(problems["password"]));
        Assert.Equal(
            "Imię jest wymagane.", Assert.Single(problems["firstName"]));
        Assert.Equal(
            "Nazwisko jest wymagane.", Assert.Single(problems["lastName"]));
    }
}
