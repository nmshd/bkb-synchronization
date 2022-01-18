using Enmeshed.BuildingBlocks.Application.Abstractions.Exceptions;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.UserContext;
using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using Enmeshed.Tooling;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Synchronization.Application.AutoMapper;
using Synchronization.Application.SyncRuns.Commands.StartSyncRun;
using Synchronization.Application.SyncRuns.DTOs;
using Synchronization.Domain.Entities.Sync;
using Synchronization.Infrastructure.Persistence.Database;
using Xunit;

namespace Synchronization.Application.Tests.Tests.SyncRuns.Commands.StartSyncRun;

public class HandlerTests
{
    private const int DATAWALLET_VERSION = 1;
    private readonly IdentityAddress _activeIdentity = TestDataGenerator.CreateRandomIdentityAddress();
    private readonly DeviceId _activeDevice = TestDataGenerator.CreateRandomDeviceId();
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;
    private readonly ApplicationDbContext _arrangeContext;
    private readonly ApplicationDbContext _assertionContext;
    private readonly ApplicationDbContext _actContext;

    public HandlerTests()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;

        var setupContext = new ApplicationDbContext(_dbOptions);
        setupContext.Database.EnsureCreated();
        setupContext.Dispose();

        _arrangeContext = CreateDbContext();
        _actContext = CreateDbContext();
        _assertionContext = CreateDbContext();

        _arrangeContext.SaveEntity(new Domain.Entities.Datawallet(new Domain.Entities.Datawallet.DatawalletVersion(DATAWALLET_VERSION), _activeIdentity));
    }

    [Fact]
    public async Task Start_a_sync_run()
    {
        // Arrange
        var handler = CreateHandler(_activeIdentity);

        var externalEvent = ExternalEventBuilder.Build().WithOwner(_activeIdentity).Create();
        _arrangeContext.SaveEntity(externalEvent);


        // Act
        var response = await handler.Handle(new StartSyncRunCommand(SyncRunDTO.SyncRunType.ExternalEventSync, DATAWALLET_VERSION), CancellationToken.None);


        // Assert
        response.Status.Should().Be(StartSyncRunStatus.Created);
        response.SyncRun.Should().NotBeNull();
    }

    [Fact]
    public async Task Starting_two_sync_runs_parallely_leads_to_error_for_one_call()
    {
        // Arrange
        var externalEvent = ExternalEventBuilder.Build().WithOwner(_activeIdentity).Create();
        _arrangeContext.SaveEntity(externalEvent);

        // By adding a save-delay to one of the calls, we can ensure that the second one will finish first, and therefore the first one
        // will definitely run into an error regarding the duplicate database index.
        var handlerWithDelayedSave = CreateHandlerWithDelayedSave(TimeSpan.FromMilliseconds(200));
        var handlerWithImmediateSave = CreateHandlerWithDelayedSave(TimeSpan.FromMilliseconds(50));


        // Act
        var taskWithImmediateSave = handlerWithImmediateSave.Handle(new StartSyncRunCommand(SyncRunDTO.SyncRunType.ExternalEventSync, DATAWALLET_VERSION), CancellationToken.None);

        var taskWithDelayedSave = handlerWithDelayedSave.Handle(new StartSyncRunCommand(SyncRunDTO.SyncRunType.ExternalEventSync, DATAWALLET_VERSION), CancellationToken.None);

        var handleWithDelayedSave = () => taskWithDelayedSave;
        var handleWithImmediateSave = () => taskWithImmediateSave;


        // Assert
        await handleWithDelayedSave
            .Should().ThrowAsync<OperationFailedException>()
            .WithMessage("Another sync run is currently active.*")
            .WithErrorCode("error.platform.validation.syncRun.cannotStartSyncRunWhenAnotherSyncRunIsRunning");

        await handleWithImmediateSave.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Cannot_start_sync_run_when_another_one_is_running()
    {
        // Arrange
        var handler = CreateHandler(_activeIdentity);

        var aRunningSyncRun = SyncRunBuilder.Build().CreatedBy(_activeIdentity).Create();
        _arrangeContext.SaveEntity(aRunningSyncRun);


        // Act
        Func<Task> acting = async () => await handler.Handle(new StartSyncRunCommand(SyncRunDTO.SyncRunType.ExternalEventSync, DATAWALLET_VERSION), CancellationToken.None);


        // Assert
        await acting.Should().ThrowAsync<OperationFailedException>().WithErrorCode("error.platform.validation.syncRun.cannotStartSyncRunWhenAnotherSyncRunIsRunning");
    }

    [Fact]
    public async Task No_sync_items_with_max_error_count_are_added()
    {
        // Arrange
        var handler = CreateHandler(_activeIdentity);

        var itemWithoutErrors = ExternalEventBuilder.Build().WithOwner(_activeIdentity).Create();
        _arrangeContext.SaveEntity(itemWithoutErrors);

        var itemWithMaxErrorCount = ExternalEventBuilder.Build().WithOwner(_activeIdentity).WithMaxErrorCount().Create();
        _arrangeContext.SaveEntity(itemWithMaxErrorCount);


        // Act
        var response = await handler.Handle(new StartSyncRunCommand(SyncRunDTO.SyncRunType.ExternalEventSync, DATAWALLET_VERSION), CancellationToken.None);


        // Assert
        var itemsOfSyncRun = _assertionContext.ExternalEvents.Where(i => i.SyncRunId == response.SyncRun.Id);
        itemsOfSyncRun.Should().Contain(i => i.Id == itemWithoutErrors.Id);
        itemsOfSyncRun.Should().NotContain(i => i.Id == itemWithMaxErrorCount.Id);
    }

    [Fact]
    public async Task No_sync_run_is_started_when_no_new_sync_items_exist()
    {
        // Arrange
        var handler = CreateHandler(_activeIdentity);


        // Act
        var response = await handler.Handle(new StartSyncRunCommand(SyncRunDTO.SyncRunType.ExternalEventSync, DATAWALLET_VERSION), CancellationToken.None);


        // Assert
        response.Status.Should().Be(StartSyncRunStatus.NoNewEvents);
        response.SyncRun.Should().BeNull();
    }

    [Fact]
    public async Task Only_sync_items_of_active_identity_are_added()
    {
        // Arrange
        var handler = CreateHandler(_activeIdentity);

        var itemOfActiveIdentity = ExternalEventBuilder.Build().WithOwner(_activeIdentity).Create();
        _arrangeContext.SaveEntity(itemOfActiveIdentity);

        var itemOfOtherIdentity = ExternalEventBuilder.Build().WithOwner(TestDataGenerator.CreateRandomIdentityAddress()).Create();
        _arrangeContext.SaveEntity(itemOfOtherIdentity);


        // Act
        var response = await handler.Handle(new StartSyncRunCommand(SyncRunDTO.SyncRunType.ExternalEventSync, 1), CancellationToken.None);


        // Assert
        var itemsOfSyncRun = _assertionContext.ExternalEvents.Where(i => i.SyncRunId == response.SyncRun.Id);
        itemsOfSyncRun.Should().Contain(i => i.Id == itemOfActiveIdentity.Id);
        itemsOfSyncRun.Should().NotContain(i => i.Id == itemOfOtherIdentity.Id);
    }

    [Fact]
    public async Task Only_unsynced_sync_items_are_added()
    {
        // Arrange
        var handler = CreateHandler(_activeIdentity);

        var unsyncedItem = _arrangeContext.SaveEntity(ExternalEventBuilder.Build().WithOwner(_activeIdentity).Create());
        var syncedItem = _arrangeContext.SaveEntity(ExternalEventBuilder.Build().WithOwner(_activeIdentity).AlreadySynced().Create());


        // Act
        var response = await handler.Handle(new StartSyncRunCommand(SyncRunDTO.SyncRunType.ExternalEventSync, DATAWALLET_VERSION), CancellationToken.None);


        // Assert
        var itemsOfSyncRun = _assertionContext.ExternalEvents.Where(i => i.SyncRunId == response.SyncRun.Id);
        itemsOfSyncRun.Should().Contain(i => i.Id == unsyncedItem.Id);
        itemsOfSyncRun.Should().NotContain(i => i.Id == syncedItem.Id);
    }

    [Fact]
    public async Task Start_a_sync_run_when_already_running_sync_run_is_expired()
    {
        // Arrange
        var handler = CreateHandler(_activeIdentity);

        var externalEvent = ExternalEventBuilder
            .Build()
            .WithOwner(_activeIdentity)
            .Create();
        _arrangeContext.SaveEntity(externalEvent);

        var expiredSyncRun = SyncRunBuilder
            .Build()
            .CreatedBy(_activeIdentity)
            .ExpiresAt(SystemTime.UtcNow.AddDays(-5))
            .WithExternalEvents(new List<ExternalEvent> {externalEvent})
            .Create();
        _arrangeContext.SaveEntity(expiredSyncRun);


        // Act
        var response = await handler.Handle(new StartSyncRunCommand(SyncRunDTO.SyncRunType.ExternalEventSync, DATAWALLET_VERSION), CancellationToken.None);


        // Assert
        response.Status.Should().Be(StartSyncRunStatus.Created);
        response.SyncRun.Should().NotBeNull();

        var canceledSyncRun = _assertionContext.SyncRuns.First(s => s.Id == expiredSyncRun.Id);
        canceledSyncRun.FinalizedAt.Should().NotBeNull();

        var externalEventOfCanceledSyncRun = _assertionContext.ExternalEvents.First(i => i.Id == externalEvent.Id);
        externalEventOfCanceledSyncRun.SyncRunId.Should().Be(response.SyncRun.Id);
        externalEventOfCanceledSyncRun.SyncErrorCount.Should().Be(1);
    }

    #region CreateHandler
    
    private ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(_dbOptions);
    }

    private Handler CreateHandlerWithDelayedSave(TimeSpan delay)
    {
        return CreateHandler(_activeIdentity, _activeDevice, CreateDbContextWithDelayedSave(delay));
    }

    private ApplicationDbContextWithDelayedSave CreateDbContextWithDelayedSave(TimeSpan delay)
    {
        return new ApplicationDbContextWithDelayedSave(_dbOptions, delay);
    }

    private Handler CreateHandler(IdentityAddress activeIdentity)
    {
        var activeDevice = TestDataGenerator.CreateRandomDeviceId();
        var handler = CreateHandler(activeIdentity, activeDevice);
        return handler;
    }

    private Handler CreateHandler(IdentityAddress activeIdentity, DeviceId createdByDevice, ApplicationDbContext dbContext = null)
    {
        var userContext = A.Fake<IUserContext>();
        A.CallTo(() => userContext.GetAddress()).Returns(activeIdentity);
        A.CallTo(() => userContext.GetDeviceId()).Returns(createdByDevice);

        var mapper = AutoMapperProfile.CreateMapper();

        return new Handler(dbContext ?? _actContext, userContext, mapper);
    }

    #endregion
}
