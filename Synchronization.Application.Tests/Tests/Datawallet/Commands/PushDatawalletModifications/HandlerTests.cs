using AutoFixture;
using Enmeshed.BuildingBlocks.Application.Abstractions.Exceptions;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.EventBus;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.Persistence.BlobStorage;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.UserContext;
using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Synchronization.Application.AutoMapper;
using Synchronization.Application.Datawallets.Commands.PushDatawalletModifications;
using Synchronization.Application.Datawallets.DTOs;
using Synchronization.Infrastructure.Persistence.Database;
using Xunit;

namespace Synchronization.Application.Tests.Tests.Datawallet.Commands.PushDatawalletModifications
{
    public class HandlerTests
    {
        private readonly IdentityAddress _activeIdentity = TestDataGenerator.CreateRandomIdentityAddress();
        private readonly DeviceId _activeDevice = TestDataGenerator.CreateRandomDeviceId();
        private readonly DbContextOptions<ApplicationDbContext> _dbOptions;
        private readonly Fixture _testDataGenerator;

        public HandlerTests()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;

            var setupContext = new DbContextWithDelayedSave(_dbOptions);
            setupContext.Database.EnsureCreated();
            setupContext.Dispose();

            _testDataGenerator = new Fixture();
            _testDataGenerator.Customize<PushDatawalletModificationItem>(composer => composer.With(m => m.DatawalletVersion, 1));
        }

        [Fact]
        public async Task Parallel_push_leads_to_an_error()
        {
            var arrangeContext = GetDbContext();

            // By adding a save-delay to one of the calls, we can ensure that the second one will finish first, and therefore the first one
            // will definitely run into an error regarding the duplicate database index.
            var actContextWithDelayedSave = GetDbContextWithDelayedSave();
            var actContextWithImmediateSave = GetDbContext();

            arrangeContext.SaveEntity(new Domain.Entities.Datawallet(new Domain.Entities.Datawallet.DatawalletVersion(1), _activeIdentity));

            var handlerWithDelayedSave = CreateHandler(_activeIdentity, _activeDevice, actContextWithDelayedSave);
            var handlerWithImmediateSave = CreateHandler(_activeIdentity, _activeDevice, actContextWithImmediateSave);

            var newModifications = _testDataGenerator.CreateMany<PushDatawalletModificationItem>(1).ToArray();
            

            // Act
            var parallelPush = () => Task.WhenAll(
                handlerWithDelayedSave.Handle(new PushDatawalletModificationsCommand(newModifications, null, 1), CancellationToken.None),
                handlerWithImmediateSave.Handle(new PushDatawalletModificationsCommand(newModifications, null, 1), CancellationToken.None)
            );


            // Assert
            await parallelPush
                .Should().ThrowAsync<OperationFailedException>()
                .WithMessage("The sent localIndex does not match the index of the latest modification.*")
                .WithErrorCode("error.platform.validation.datawallet.datawalletNotUpToDate");
        }

        private DbContextWithDelayedSave GetDbContextWithDelayedSave()
        {
            return new DbContextWithDelayedSave(_dbOptions);
        }

        private ApplicationDbContext GetDbContext()
        {
            return new ApplicationDbContext(_dbOptions);
        }

        private static Handler CreateHandler(IdentityAddress activeIdentity, DeviceId activeDevice, ApplicationDbContext dbContext)
        {
            var userContext = A.Fake<IUserContext>();
            A.CallTo(() => userContext.GetAddress()).Returns(activeIdentity);
            A.CallTo(() => userContext.GetDeviceId()).Returns(activeDevice);

            var blobStorage = A.Fake<IBlobStorage>();

            var mapper = AutoMapperProfile.CreateMapper();

            var eventBus = A.Fake<IEventBus>();

            return new Handler(dbContext, userContext, mapper, blobStorage, eventBus);
        }
    }

    public class DbContextWithDelayedSave : ApplicationDbContext
    {
        private readonly int _delayInMilliseconds;

        public DbContextWithDelayedSave(DbContextOptions<ApplicationDbContext> options, int delayInMilliseconds = 100) : base(options)
        {
            _delayInMilliseconds = delayInMilliseconds;
        }


        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new())
        {
            await Task.Delay(_delayInMilliseconds, cancellationToken);

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
