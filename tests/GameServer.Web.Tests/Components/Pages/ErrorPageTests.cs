using Bunit;
using GameServer.Web.Components.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace GameServer.Web.Tests.Components.Pages;

public class ErrorPageTests : BunitContext
{
    [Fact]
    public void ErrorPage_RendersWithoutRequestIdByDefault()
    {
        var cut = Render<Error>();
        Assert.Contains("Error.", cut.Markup);
        Assert.Contains("An error occurred while processing your request.", cut.Markup);
        Assert.DoesNotContain("Request ID:", cut.Markup);
    }

    [Fact]
    public void ErrorPage_WithActivity_RendersRequestId()
    {
        var activity = new Activity("TestActivity");
        activity.Start();
        try
        {
            var cut = Render<Error>();
            Assert.Contains("Request ID:", cut.Markup);
            Assert.Contains(activity.Id!, cut.Markup);
        }
        finally
        {
            activity.Stop();
        }
    }

    [Fact]
    public void ErrorPage_WithHttpContext_RendersTraceIdentifier()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-id-12345"
        };

        var cut = Render<Error>(parameters => parameters
            .AddCascadingValue(httpContext));

        Assert.Contains("Request ID:", cut.Markup);
        Assert.Contains("trace-id-12345", cut.Markup);
    }
}
