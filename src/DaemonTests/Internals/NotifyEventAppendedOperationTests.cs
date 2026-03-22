using JasperFx.Events;
using Marten.Events.Operations;
using Marten.Internal.Operations;
using Shouldly;
using Xunit;

namespace DaemonTests.Internals;

public class NotifyEventAppendedOperationTests
{
    [Fact]
    public void has_correct_operation_role()
    {
        var operation = new NotifyEventAppendedOperation();
        operation.Role().ShouldBe(OperationRole.Events);
    }

    [Fact]
    public void document_type_is_IEvent()
    {
        var operation = new NotifyEventAppendedOperation();
        operation.DocumentType.ShouldBe(typeof(IEvent));
    }
}
