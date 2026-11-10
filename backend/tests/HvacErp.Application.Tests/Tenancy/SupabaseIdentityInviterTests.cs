using System.Net;
using System.Text;
using HvacErp.Application.Common;
using HvacErp.Infrastructure.Auth;

namespace HvacErp.Application.Tests.Tenancy;

/// <summary>The invitation request sent to Supabase Auth and how its answers are read.</summary>
public class SupabaseIdentityInviterTests
{
    private HttpRequestMessage? _request;
    private string? _requestBody;

    private SupabaseIdentityInviter Inviter(HttpStatusCode status, string body) =>
        new(new HttpClient(new Handler(async request =>
        {
            _request = request;
            _requestBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        })) { BaseAddress = new Uri("https://project.supabase.co/") });

    [Fact]
    public async Task An_invitation_posts_the_email_with_the_landing_page_and_returns_the_new_account_id()
    {
        var sut = Inviter(HttpStatusCode.OK, """{"id":"6f1c0e2a-0b7e-4a5e-9c53-1d2b3c4d5e6f","email":"new@example.com","invited_at":"2026-11-10T10:00:00Z"}""");

        var id = await sut.InviteAsync("new@example.com", "https://app.example/auth/set-password");

        Assert.Equal(Guid.Parse("6f1c0e2a-0b7e-4a5e-9c53-1d2b3c4d5e6f"), id);
        Assert.Equal(HttpMethod.Post, _request!.Method);
        Assert.Equal("https://project.supabase.co/auth/v1/invite?redirect_to=https%3A%2F%2Fapp.example%2Fauth%2Fset-password", _request.RequestUri!.AbsoluteUri);
        Assert.Equal("""{"email":"new@example.com"}""", _requestBody);
    }

    [Fact]
    public async Task Without_a_landing_page_the_providers_default_is_used()
    {
        await Inviter(HttpStatusCode.OK, """{"id":"6f1c0e2a-0b7e-4a5e-9c53-1d2b3c4d5e6f"}""").InviteAsync("new@example.com", null);

        Assert.Equal("/auth/v1/invite", _request!.RequestUri!.PathAndQuery);
    }

    [Theory]
    [InlineData("""{"code":422,"error_code":"email_exists","msg":"A user with this email address has already been registered"}""")]
    [InlineData("""{"code":422,"msg":"A user with this email address has already been registered"}""")]
    public async Task An_existing_account_is_a_conflict(string body)
    {
        await Assert.ThrowsAsync<ConflictException>(
            () => Inviter(HttpStatusCode.UnprocessableEntity, body).InviteAsync("known@example.com", null));
    }

    [Fact]
    public async Task A_refused_address_is_reported_in_the_providers_words()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Inviter(HttpStatusCode.BadRequest, """{"code":400,"error_code":"validation_failed","msg":"Unable to validate email address: invalid format"}""")
                .InviteAsync("nope", null));

        Assert.Contains("invalid format", error.Message);
    }

    [Fact]
    public async Task Too_many_invitations_asks_to_wait()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Inviter(HttpStatusCode.TooManyRequests, """{"error_code":"over_email_send_rate_limit","msg":"email rate limit exceeded"}""").InviteAsync("a@example.com", null));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_bad_key_or_an_outage_is_a_server_error_not_the_users_mistake(HttpStatusCode status)
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => Inviter(status, "{}").InviteAsync("a@example.com", null));
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
