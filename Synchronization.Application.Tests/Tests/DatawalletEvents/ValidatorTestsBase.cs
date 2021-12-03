using Enmeshed.Tooling;
using Enmeshed.UnitTestTools.BaseClasses;

namespace Synchronization.Application.Tests.Tests.DatawalletEvents
{
    public abstract class ValidatorTestsBase : AbstractTestsBase
    {
        protected ValidatorTestsBase()
        {
            SystemTime.Set(_dateTimeNow);
        }
    }
}
