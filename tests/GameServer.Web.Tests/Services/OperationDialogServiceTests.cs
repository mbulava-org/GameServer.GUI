using Bunit;
using GameServer.Web.Models;
using GameServer.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Xunit;

namespace GameServer.Web.Tests.Services;

public class OperationDialogServiceTests : BunitContext
{
    private readonly DialogService _dialogService;
    private readonly OperationDialogService _operationDialog;

    public OperationDialogServiceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<DialogService>();
        _dialogService = Services.GetRequiredService<DialogService>();
        _operationDialog = new OperationDialogService(_dialogService);
    }

    [Fact]
    public async Task ExecuteAsync_ActionSucceeds_ReturnsSuccessResult()
    {
        var executed = false;

        var result = await _operationDialog.ExecuteAsync("Starting Service...", "Please wait", async () =>
        {
            await Task.Yield();
            executed = true;
        });

        Assert.True(executed);
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ActionThrowsException_ReturnsFailureResultWithExceptionDetails()
    {
        var result = await _operationDialog.ExecuteAsync("Starting Service...", "Please wait", () =>
        {
            throw new InvalidOperationException("Docker daemon is unreachable");
        });

        Assert.False(result.Success);
        Assert.Equal("Docker daemon is unreachable", result.ErrorMessage);
        Assert.IsType<InvalidOperationException>(result.Exception);
    }

    [Fact]
    public async Task ExecuteAsync_GenericFunctionSucceeds_ReturnsSuccessResultWithData()
    {
        var result = await _operationDialog.ExecuteAsync<int>("Calculating...", null, new Func<Task<int>>(async () =>
        {
            await Task.Yield();
            return 42;
        }));

        Assert.True(result.Success);
        Assert.Equal(42, result.Data);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_GenericFunctionThrowsException_ReturnsFailureResult()
    {
        var result = await _operationDialog.ExecuteAsync<int>("Fetching...", null, new Func<Task<int>>(() =>
        {
            throw new HttpRequestException("404 Not Found");
        }));

        Assert.False(result.Success);
        Assert.Equal(0, result.Data);
        Assert.Equal("404 Not Found", result.ErrorMessage);
        Assert.IsType<HttpRequestException>(result.Exception);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOperationResultOk_ReturnsSuccess()
    {
        var result = await _operationDialog.ExecuteAsync("Deleting...", null, async () =>
        {
            await Task.Yield();
            return OperationResult.Ok();
        });

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOperationResultFail_ReturnsFailureResult()
    {
        var result = await _operationDialog.ExecuteAsync("Deleting...", null, async () =>
        {
            await Task.Yield();
            return OperationResult.Fail("User is assigned to active servers");
        });

        Assert.False(result.Success);
        Assert.Equal("User is assigned to active servers", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_GenericOperationResultOk_ReturnsData()
    {
        var result = await _operationDialog.ExecuteAsync<string>("Saving...", null, async () =>
        {
            await Task.Yield();
            return OperationResult<string>.Ok("saved-id-123");
        });

        Assert.True(result.Success);
        Assert.Equal("saved-id-123", result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_GenericOperationResultFail_ReturnsFailure()
    {
        var result = await _operationDialog.ExecuteAsync<string>("Saving...", null, async () =>
        {
            await Task.Yield();
            return OperationResult<string>.Fail("Validation failed");
        });

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal("Validation failed", result.ErrorMessage);
    }
}
