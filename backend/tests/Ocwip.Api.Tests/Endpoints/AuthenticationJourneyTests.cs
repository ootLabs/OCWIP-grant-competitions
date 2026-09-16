using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-12.6: one scenario walking the whole authentication path over real HTTP
/// on a fresh PostgreSQL database, the way a single applicant would live it end
/// to end: register, confirm the address, sign in, sign out, forget the
/// password, reset it and sign in again with the new one.
///
/// T-12.1 through T-12.5 already cover every one of these steps in isolation,
/// negative cases and error formats included - repeating that here would only
/// make this test slow. What only one connected run can catch is the seam
/// between two cards: the confirmation link /register actually sends, the
/// cookie /login actually issues, the account /reset-password actually leaves
/// behind. One test, so it stays fast enough to run on every change.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthenticationJourneyTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public AuthenticationJourneyTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private WebApplicationFactory<Program> CreateHost() =>
        SessionTestHost.Create(
            _factory,
            _database,
            services: services =>
                services.AddSingleton<IEmailSender, RecordingEmailSender>());

    /// <summary>
    /// Parses the link a real recipient would click out of a plain text email,
    /// the same way EmailVerificationEndpointsTests and PasswordResetEndpointTests do.
    /// </summary>
    private static (string UserId, string Token) ExtractLink(string emailBody)
    {
        var url = Regex.Match(emailBody, @"https?://\S+").Value;
        Assert.NotEmpty(url);

        var query = QueryHelpers.ParseQuery(new Uri(url).Query);
        return (query["userId"].ToString(), query["token"].ToString());
    }

    [RequiresDatabaseFact]
    public async Task Registration_through_reset_password_login_forms_one_connected_path()
    {
        var host = CreateHost();
        // Raw, so the cookie /login issues has to be carried by hand - the
        // point of the logout step below is that the SAME cookie stops working,
        // which a client that quietly drops it on its own would hide.
        var client = SessionTestHost.RawClient(host);
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = SessionTestHost.Email("sciezka");

        // Rejestracja.
        var register = await client.PostAsJsonAsync(
            "/register",
            new RegisterRequest(email, SessionTestHost.Password, "Ada", "Testowa"));
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);

        // Weryfikacja maila.
        var verificationEmail = Assert.Single(emails.Sent, m => m.To == email);
        var (verifyUserId, verifyToken) = ExtractLink(verificationEmail.Body);
        var verify = await client.PostAsJsonAsync(
            "/verify-email", new VerifyEmailRequest(verifyUserId, verifyToken));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        // Logowanie.
        var login = await client.PostAsJsonAsync(
            "/login", new LoginRequest(email, SessionTestHost.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var sessionCookie = SessionTestHost.SessionCookie(login);

        var meWhileSignedIn = await client.SendAsync(
            SessionTestHost.WithCookie(HttpMethod.Get, "/me", sessionCookie));
        Assert.Equal(HttpStatusCode.OK, meWhileSignedIn.StatusCode);

        // Wylogowanie.
        var logout = await client.SendAsync(
            SessionTestHost.WithCookie(HttpMethod.Post, "/logout", sessionCookie));
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var meAfterLogout = await client.SendAsync(
            SessionTestHost.WithCookie(HttpMethod.Get, "/me", sessionCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterLogout.StatusCode);

        // Reset hasła.
        var forgotPassword = await client.PostAsJsonAsync(
            "/forgot-password", new ForgotPasswordRequest(email));
        Assert.Equal(HttpStatusCode.OK, forgotPassword.StatusCode);

        var resetEmail = Assert.Single(
            emails.Sent, m => m.To == email && m.Body.Contains("/reset-password"));
        var (resetUserId, resetToken) = ExtractLink(resetEmail.Body);

        const string newPassword = "Nowe-Haslo1";
        var resetPassword = await client.PostAsJsonAsync(
            "/reset-password",
            new ResetPasswordRequest(resetUserId, resetToken, newPassword));
        Assert.Equal(HttpStatusCode.OK, resetPassword.StatusCode);

        // Logowanie nowym hasłem.
        var loginWithNewPassword = await client.PostAsJsonAsync(
            "/login", new LoginRequest(email, newPassword));
        Assert.Equal(HttpStatusCode.OK, loginWithNewPassword.StatusCode);
    }
}
