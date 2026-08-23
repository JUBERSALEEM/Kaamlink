using KaamLink.Application.Common;
using KaamLink.Application.Companies;

namespace KaamLink.UnitTests.Companies;

public sealed class CompanyServiceTests
{
    private static (AuthServiceHarness.Context Harness, CompanyService Service) Setup()
    {
        var harness = AuthServiceHarness.Create();
        var service = new CompanyService(
            harness.Companies,
            harness.UnitOfWork,
            harness.Audit,
            harness.Time);
        return (harness, service);
    }

    private static readonly CreateCompanyRequest ValidRequest = new()
    {
        Name = "Gulf Fabrication LLC",
        Category = "Workshop",
        Emirate = "Sharjah",
        Area = "Industrial Area 12",
        TradeLicenseNumber = "CN-1234567"
    };

    [Fact]
    public async Task Create_MakesCompanyPending_AndCallerOwner()
    {
        var (harness, service) = Setup();
        var userId = Guid.NewGuid();

        var created = await service.CreateAsync(userId, ValidRequest);

        Assert.Equal("Pending", created.VerificationState);
        Assert.Equal("Owner", created.Role);
        Assert.Equal(ValidRequest.Name, created.Name);
        Assert.Null(created.VerifiedAt);

        // The membership round-trip (as used by /me) carries the company.
        var mine = await service.GetMineAsync(userId);
        var entry = Assert.Single(mine);
        Assert.Equal(created.Id, entry.Id);
        Assert.Equal("Owner", entry.Role);

        Assert.Contains(harness.Audit.Records, r => r.Action == KaamLink.Domain.Auditing.AuditActions.CompanyRegistered);
    }

    [Fact]
    public async Task AdminReview_Approve_SetsVerified()
    {
        var (harness, service) = Setup();
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var created = await service.CreateAsync(ownerId, ValidRequest);

        var reviewed = await service.ReviewAsync(adminId, created.Id, new ReviewCompanyRequest { Approve = true });

        Assert.Equal("Verified", reviewed.VerificationState);
        Assert.NotNull(reviewed.VerifiedAt);
        Assert.Contains(harness.Audit.Records, r => r.Action == KaamLink.Domain.Auditing.AuditActions.CompanyVerified);
    }

    [Fact]
    public async Task AdminReview_Reject_RequiresReason()
    {
        var (_, service) = Setup();
        var created = await service.CreateAsync(Guid.NewGuid(), ValidRequest);

        var noReason = await Assert.ThrowsAsync<AppException>(() => service.ReviewAsync(
            Guid.NewGuid(), created.Id, new ReviewCompanyRequest { Approve = false }));
        Assert.Equal("company.reason_required", noReason.Code);

        var rejected = await service.ReviewAsync(
            Guid.NewGuid(), created.Id, new ReviewCompanyRequest { Approve = false, Reason = "Licence document unreadable" });
        Assert.Equal("Rejected", rejected.VerificationState);
        Assert.Equal("Licence document unreadable", rejected.VerificationReason);
    }

    [Fact]
    public async Task Review_Twice_IsRejected()
    {
        var (_, service) = Setup();
        var created = await service.CreateAsync(Guid.NewGuid(), ValidRequest);
        await service.ReviewAsync(Guid.NewGuid(), created.Id, new ReviewCompanyRequest { Approve = true });

        var exception = await Assert.ThrowsAsync<AppException>(() => service.ReviewAsync(
            Guid.NewGuid(), created.Id, new ReviewCompanyRequest { Approve = false, Reason = "second look" }));

        Assert.Equal("company.already_reviewed", exception.Code);
    }

    [Fact]
    public async Task PendingQueue_ListsOnlyPending_OldestFirst()
    {
        var (_, service) = Setup();
        var first = await service.CreateAsync(Guid.NewGuid(), ValidRequest);
        await service.CreateAsync(Guid.NewGuid(), ValidRequest with { Name = "Second Co" });

        var queue = await service.ListPendingAsync();

        Assert.Equal(2, queue.Count);
        Assert.Equal(first.Id, queue[0].Id);
        Assert.Equal(string.Empty, queue[0].Role); // queue rows carry no membership (Role empty)
    }

    [Fact]
    public async Task FourthOwnedCompany_IsRejected()
    {
        var (_, service) = Setup();
        var ownerId = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
            await service.CreateAsync(ownerId, ValidRequest with { Name = $"Co {i}" });

        var exception = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(ownerId, ValidRequest));

        Assert.Equal("company.too_many", exception.Code);
    }
}
