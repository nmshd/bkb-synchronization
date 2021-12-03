using AutoMapper;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.EventBus;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.UserContext;
using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using Moq;
using Synchronization.Application.AutoMapper;
using Enmeshed.Tooling;
using Enmeshed.UnitTestTools.BaseClasses;
using Synchronization.Infrastructure.Persistence.Database;

namespace Synchronization.Application.Tests.Tests.DatawalletEvents
{
    public abstract class HandlerTestsBase : RequestHandlerTestsBase<ApplicationDbContext>
    {
        protected static readonly IdentityAddress ActiveIdentity = IdentityAddress.Parse(TestData.IdentityAddresses.ADDRESS_1);
        protected static readonly DeviceId ActiveDevice = DeviceId.Parse("DVCactivedevice");
        protected readonly Mock<IEventBus> _eventBusMock;
        protected readonly IMapper _mapper;

        protected readonly Mock<IUserContext> _userContextMock;

        protected HandlerTestsBase()
        {
            SystemTime.Set(_dateTimeNow);

            _eventBusMock = new Mock<IEventBus>();

            _userContextMock = new Mock<IUserContext>();
            _userContextMock.Setup(s => s.GetAddress()).Returns(ActiveIdentity);
            _userContextMock.Setup(s => s.GetDeviceId()).Returns(ActiveDevice);

            _mapper = AutoMapperProfile.CreateMapper();
        }
    }
}
