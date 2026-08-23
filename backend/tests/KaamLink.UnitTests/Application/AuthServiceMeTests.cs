using KaamLink.Application.Auth;
using KaamLink.Application.Common;

namespace KaamLink.UnitTests.Application;

public sealed class AuthServiceMeTests
{
    private const string Phone = "+971501112223";

    [Fact]
    public async Task GetMe_ReturnsCurrentIdentity()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        var login = await service.VerifyOtpAsync(
            new VerifyOtpRequest { Phone = Phone, Code = await harness.RequestOtpAndCaptureCodeAsync(Phone) });

        var me = await service.GetMeAsync(login.User.Id);

        Assert.Equal(login.User.Id, me.Id);
        Assert.Equal(Phone, me.PhoneNumber);
        Assert.Equal("Active", me.Status);
        Assert.False(me.IsPlatformAdmin);
    }

    [Fact]
    public async Task GetMe_UnknownId_ThrowsUnauthorized()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();

        var exception = await Assert.ThrowsAsync<AppException>(() => service.GetMeAsync(Guid.NewGuid()));

        Assert.Equal("auth.user_not_found", exception.Code);
        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public async Task GetMe_BlockedUser_IsForbidden()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        var login = await service.VerifyOtpAsync(
            new VerifyOtpRequest { Phone = Phone, Code = await harness.RequestOtpAndCaptureCodeAsync(Phone) });

        harness.Users.Store.Single().Block(harness.Time.UtcNow);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.GetMeAsync(login.User.Id));

        Assert.Equal("auth.account_blocked", exception.Code);
        Assert.Equal(403, exception.StatusCode);
    }
}
