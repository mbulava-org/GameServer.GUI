using GameServer.Web.Models;

namespace GameServer.Web.Tests.Models;

public class OperationResultTests
{
    [Fact]
    public void Ok_ShouldCreateSuccessfulResult()
    {
        var result = OperationResult.Ok();

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Fail_ShouldCreateFailedResultWithMessageAndException()
    {
        var ex = new InvalidOperationException("boom");
        var result = OperationResult.Fail("An error occurred", ex);

        Assert.False(result.Success);
        Assert.Equal("An error occurred", result.ErrorMessage);
        Assert.Same(ex, result.Exception);
    }

    [Fact]
    public void GenericOk_ShouldHoldDataAndIndicateSuccess()
    {
        var result = OperationResult<int>.Ok(123);

        Assert.True(result.Success);
        Assert.Equal(123, result.Data);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void GenericFail_ShouldHoldErrorMessageAndIndicateFailure()
    {
        var result = OperationResult<string>.Fail("Failed to fetch");

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal("Failed to fetch", result.ErrorMessage);
    }

    [Fact]
    public void ImplicitOperator_FromData_ShouldProduceSuccessfulGenericResult()
    {
        OperationResult<string> result = "hello";

        Assert.True(result.Success);
        Assert.Equal("hello", result.Data);
    }
}
