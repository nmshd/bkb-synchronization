using Enmeshed.BuildingBlocks.Application.Abstractions.Exceptions;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using FluentAssertions.Specialized;

namespace Synchronization.Application.Tests
{
    public static class ApplicationExceptionExtensions
    {
        public static ApplicationExceptionAssertions Should(this ApplicationException instance)
        {
            return new ApplicationExceptionAssertions(instance);
        }
    }

    public class ApplicationExceptionAssertions :
        ReferenceTypeAssertions<ApplicationException, ApplicationExceptionAssertions>
    {
        public ApplicationExceptionAssertions(ApplicationException instance)
        {
            Subject = instance;
        }

        protected override string Identifier => "ApplicationException";

        public AndConstraint<ApplicationExceptionAssertions> HaveErrorCode(string errorCode, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject.Code)
                .ForCondition(code => code == errorCode)
                .FailWith("Expected {context:Code} to be {0}{reason}, but found {1}.",
                    _ => errorCode, code => code);

            return new AndConstraint<ApplicationExceptionAssertions>(this);
        }
    }

    public static class ExceptionAssertionsExtensions
    {
        public static void WithErrorCode<T>(this ExceptionAssertions<T> assertions, string code) where T : ApplicationException
        {
            assertions.Which.Code.Should().Be(code);
        }
    }
}