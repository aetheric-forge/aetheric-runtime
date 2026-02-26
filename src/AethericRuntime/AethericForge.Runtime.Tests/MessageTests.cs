using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Util;

namespace AethericForge.Runtime.Tests;

public class MessageTests
{
    [Fact]
    public void Id_When_Null_Is_Assigned_New_Guid()
    {
        var msg = new TestModels.TestMessage("x.y");
        Assert.NotEqual(Guid.Empty, msg.Id);
    }

    [Fact]
    public void Id_When_Provided_Is_Preserved()
    {
        var id = Guid.NewGuid();
        var msg = new TestModels.TestMessage(id, "x.y");
        Assert.Equal(id, msg.Id);
    }
}
