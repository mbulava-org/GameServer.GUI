using GameServer.Docker.Constants;

namespace GameServer.Docker.Tests.Constants;

public class ServiceLabelsTests
{
    [Fact]
    public void ServiceLabels_ShouldHaveCorrectManagedLabelKey()
    {
        Assert.Equal("gameserver.docker.managed", ServiceLabels.Managed);
    }

    [Fact]
    public void ServiceLabels_ShouldHaveCorrectServerIdLabelKey()
    {
        Assert.Equal("gameserver.docker.Id", ServiceLabels.ServerId);
    }

    [Fact]
    public void ServiceLabels_ShouldHaveCorrectNameLabelKey()
    {
        Assert.Equal("gameserver.docker.name", ServiceLabels.Name);
    }

    [Fact]
    public void ServiceLabels_ShouldHaveCorrectDescriptionLabelKey()
    {
        Assert.Equal("gameserver.docker.description", ServiceLabels.Description);
    }

    [Fact]
    public void ServiceLabels_ShouldHaveCorrectGameTypeLabelKey()
    {
        Assert.Equal("gameserver.docker.gametype", ServiceLabels.GameType);
    }

    [Fact]
    public void ServiceLabels_ShouldHaveTrueAsManagedValue()
    {
        Assert.Equal("true", ServiceLabels.ManagedValue);
    }

    [Fact]
    public void ServiceLabels_AllConstantsShouldNotBeNull()
    {
        Assert.NotNull(ServiceLabels.Managed);
        Assert.NotNull(ServiceLabels.ServerId);
        Assert.NotNull(ServiceLabels.Name);
        Assert.NotNull(ServiceLabels.Description);
        Assert.NotNull(ServiceLabels.GameType);
        Assert.NotNull(ServiceLabels.ManagedValue);
    }

    [Fact]
    public void ServiceLabels_AllConstantsShouldNotBeEmpty()
    {
        Assert.NotEmpty(ServiceLabels.Managed);
        Assert.NotEmpty(ServiceLabels.ServerId);
        Assert.NotEmpty(ServiceLabels.Name);
        Assert.NotEmpty(ServiceLabels.Description);
        Assert.NotEmpty(ServiceLabels.GameType);
        Assert.NotEmpty(ServiceLabels.ManagedValue);
    }
}
