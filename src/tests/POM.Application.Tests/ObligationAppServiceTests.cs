using System.Threading;
using Moq;
using POM.Obligations;
using POM.Obligations.ExtraFields;
using POM.Obligations.Ports;
using POM.Taxonomy;
using POM.Taxonomy.Ports;
using Xunit;

namespace POM.Application.Tests.Obligations;

public sealed class ObligationAppServiceTests
{
    private readonly Mock<IObligationRepository> _obligationRepo = new();
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly IExtraFieldsValidator _validator = new ExtraFieldsValidator();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly DateTime _dueDate = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private ObligationAppService CreateService() =>
        new(_obligationRepo.Object, _categoryRepo.Object, _validator);

    [Fact]
    public async Task CreateAsync_validates_category_ownership()
    {
        _categoryRepo.Setup(r => r.GetOwnedOrDefaultAsync(_userId, _categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var service = CreateService();
        var request = new ObligationCreateRequest(ObligationType.Task, "Test", _dueDate, null, null, null, ObligationPriority.Medium, _categoryId);

        var result = await service.CreateAsync(_userId, request);

        Assert.False(result.Succeeded);
        Assert.Equal("validation_error", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_succeeds_with_valid_category()
    {
        var category = Category.CreateUser(_userId, "Test Cat", null);
        _categoryRepo.Setup(r => r.GetOwnedOrDefaultAsync(_userId, _categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var service = CreateService();
        var request = new ObligationCreateRequest(ObligationType.Task, "Test", _dueDate, null, null, null, ObligationPriority.Medium, _categoryId);

        var result = await service.CreateAsync(_userId, request);

        Assert.True(result.Succeeded);
        _obligationRepo.Verify(r => r.AddAsync(It.IsAny<Obligation>(), It.IsAny<CancellationToken>()), Times.Once);
        _obligationRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_validates_extra_fields()
    {
        var service = CreateService();
        var request = new ObligationCreateRequest(ObligationType.Payment, "Test", _dueDate, null, null, null, ObligationPriority.Medium, null, "{}");

        var result = await service.CreateAsync(_userId, request);

        Assert.False(result.Succeeded);
        Assert.Equal("validation_error", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_accepts_null_due_date()
    {
        var service = CreateService();
        var request = new ObligationCreateRequest(ObligationType.Task, "Test", null);

        var result = await service.CreateAsync(_userId, request);

        Assert.True(result.Succeeded);
        Assert.Null(result.Obligation!.DueDate);
    }

    [Fact]
    public async Task UpdateAsync_rejects_terminal_states()
    {
        var obligation = Obligation.Create(_userId, ObligationType.Task, "Test", _dueDate);
        obligation.Complete();
        _obligationRepo.Setup(r => r.GetByIdAsync(_userId, obligation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(obligation);

        var service = CreateService();
        var request = new ObligationUpdateRequest("Updated", _dueDate);

        var result = await service.UpdateAsync(_userId, obligation.Id, request);

        Assert.False(result.Succeeded);
        Assert.Equal("conflict", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_succeeds_for_pending_obligation()
    {
        var obligation = Obligation.Create(_userId, ObligationType.Task, "Test", _dueDate);
        _obligationRepo.Setup(r => r.GetByIdAsync(_userId, obligation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(obligation);
        _obligationRepo.Setup(r => r.UpdateAsync(_userId, It.IsAny<Obligation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        var request = new ObligationUpdateRequest("Updated", _dueDate.AddDays(1));

        var result = await service.UpdateAsync(_userId, obligation.Id, request);

        Assert.True(result.Succeeded);
        Assert.Equal("Updated", result.Obligation!.Title);
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_obligation()
    {
        var obligation = Obligation.Create(_userId, ObligationType.Task, "Test", _dueDate);
        _obligationRepo.Setup(r => r.GetByIdAsync(_userId, obligation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(obligation);
        _obligationRepo.Setup(r => r.UpdateAsync(_userId, It.IsAny<Obligation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        var result = await service.DeleteAsync(_userId, obligation.Id);

        Assert.True(result.Succeeded);
        Assert.NotNull(obligation.DeletedAt);
    }

    [Fact]
    public async Task CompleteAsync_transitions_to_completed()
    {
        var obligation = Obligation.Create(_userId, ObligationType.Task, "Test", _dueDate);
        _obligationRepo.Setup(r => r.GetByIdAsync(_userId, obligation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(obligation);
        _obligationRepo.Setup(r => r.UpdateAsync(_userId, It.IsAny<Obligation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        var result = await service.CompleteAsync(_userId, obligation.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(ObligationStatus.Completed, obligation.Status);
    }

    [Fact]
    public async Task SkipAsync_transitions_to_skipped()
    {
        var obligation = Obligation.Create(_userId, ObligationType.Task, "Test", _dueDate);
        _obligationRepo.Setup(r => r.GetByIdAsync(_userId, obligation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(obligation);
        _obligationRepo.Setup(r => r.UpdateAsync(_userId, It.IsAny<Obligation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        var result = await service.SkipAsync(_userId, obligation.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(ObligationStatus.Skipped, obligation.Status);
    }

    [Fact]
    public async Task ArchiveAsync_transitions_to_archived()
    {
        var obligation = Obligation.Create(_userId, ObligationType.Task, "Test", _dueDate);
        _obligationRepo.Setup(r => r.GetByIdAsync(_userId, obligation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(obligation);
        _obligationRepo.Setup(r => r.UpdateAsync(_userId, It.IsAny<Obligation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        var result = await service.ArchiveAsync(_userId, obligation.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(ObligationStatus.Archived, obligation.Status);
    }

    [Fact]
    public async Task PostponeAsync_updates_due_date_and_resets_overdue()
    {
        var obligation = Obligation.Create(_userId, ObligationType.Task, "Test", _dueDate);
        obligation.MarkOverdue();
        _obligationRepo.Setup(r => r.GetByIdAsync(_userId, obligation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(obligation);
        _obligationRepo.Setup(r => r.UpdateAsync(_userId, It.IsAny<Obligation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        var request = new ObligationPostponeRequest(_dueDate.AddDays(5));
        var result = await service.PostponeAsync(_userId, obligation.Id, request);

        Assert.True(result.Succeeded);
        Assert.Equal(ObligationStatus.Pending, obligation.Status);
        Assert.Equal(_dueDate.AddDays(5), obligation.DueDate);
    }

    [Fact]
    public async Task PostponeAsync_rejects_when_no_due_date()
    {
        var obligation = Obligation.Create(_userId, ObligationType.Task, "Test", dueDate: null);
        _obligationRepo.Setup(r => r.GetByIdAsync(_userId, obligation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(obligation);

        var service = CreateService();
        var request = new ObligationPostponeRequest(_dueDate.AddDays(5));
        var result = await service.PostponeAsync(_userId, obligation.Id, request);

        Assert.False(result.Succeeded);
        Assert.Equal("validation_error", result.ErrorCode);
    }

    [Fact]
    public async Task RestoreAsync_restores_soft_deleted()
    {
        var obligation = Obligation.Create(_userId, ObligationType.Task, "Test", _dueDate);
        obligation.SoftDelete();
        _obligationRepo.Setup(r => r.GetByIdIncludingDeletedAsync(_userId, obligation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(obligation);
        _obligationRepo.Setup(r => r.UpdateIncludingDeletedAsync(_userId, It.IsAny<Obligation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        var result = await service.RestoreAsync(_userId, obligation.Id);

        Assert.True(result.Succeeded);
        Assert.Null(obligation.DeletedAt);
    }

    [Fact]
    public async Task RestoreAsync_rejects_non_deleted()
    {
        var obligation = Obligation.Create(_userId, ObligationType.Task, "Test", _dueDate);
        _obligationRepo.Setup(r => r.GetByIdIncludingDeletedAsync(_userId, obligation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(obligation);

        var service = CreateService();
        var result = await service.RestoreAsync(_userId, obligation.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("conflict", result.ErrorCode);
    }

    [Fact]
    public async Task ListAsync_applies_filters_and_pagination()
    {
        var obligations = new[]
        {
            Obligation.Create(_userId, ObligationType.Task, "Task A", _dueDate),
            Obligation.Create(_userId, ObligationType.Payment, "Payment B", _dueDate.AddDays(5)),
            Obligation.Create(_userId, ObligationType.Task, "Task C", _dueDate.AddDays(10))
        };
        obligations[2].Complete();

        var pageResult = new PagedResult<Obligation>(obligations, 3, 1, 20);
        _obligationRepo.Setup(r => r.ListAsync(_userId, It.IsAny<ObligationListFilter>(), 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageResult);

        var service = CreateService();
        var request = new ObligationListRequest { Page = 1, PageSize = 20 };
        var result = await service.ListAsync(_userId, request);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Response!.TotalCount);
    }
}