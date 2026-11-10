using Crawl.Ai.LoginAssist;

namespace Crawl.Ai.Tests;

public class LoginAssistTests
{
    [Fact]
    public void DetectForm_finds_password_and_guided_steps()
    {
        var html = """
            <form action="/login" method="post">
              <input type="text" name="username" />
              <input type="password" name="password" />
              <input type="submit" value="Sign in" />
            </form>
            """;

        var d = LoginAssistService.DetectForm(html);
        Assert.True(d.Detected);
        Assert.Equal("/login", d.FormAction);
        Assert.Contains(d.Fields, f => f.IsPassword);
        Assert.DoesNotContain(d.Fields, f => f.Type == "submit");
        Assert.NotEmpty(d.GuidedSteps);
    }

    [Fact]
    public async Task Confirm_rejects_raw_password_and_audits()
    {
        var svc = new LoginAssistService();
        var jobId = Guid.NewGuid();
        var detect = await svc.DetectAsync(new(
            jobId,
            "https://payer.example/login",
            "<form><input type='password' name='password' /></form>"));

        var rejected = await svc.ConfirmStepAsync(new(
            jobId,
            detect.AuditEventId,
            1,
            true,
            "SuperSecretPassword1",
            "op-1"));

        Assert.Equal("rejected", rejected.Status);
        Assert.Contains(svc.GetAuditTrail(jobId), e => e.Kind.Contains("rejected"));
        Assert.All(svc.GetAuditTrail(jobId), e =>
        {
            Assert.DoesNotContain("SuperSecretPassword1", e.Detail);
            Assert.True(e.CredentialVaultRef is null || e.CredentialVaultRef.StartsWith("vault:"));
        });
    }

    [Fact]
    public async Task Confirm_accepts_vault_ref()
    {
        var svc = new LoginAssistService();
        var jobId = Guid.NewGuid();
        var detect = await svc.DetectAsync(new(jobId, null, "<form><input type='password' name='p'/></form>"));
        var ok = await svc.ConfirmStepAsync(new(jobId, detect.AuditEventId, 0, true, "vault:payer/login", "op"));
        Assert.Equal("approved", ok.Status);
        Assert.Contains(svc.GetAuditTrail(jobId), e => e.CredentialVaultRef == "vault:payer/login");
    }
}
