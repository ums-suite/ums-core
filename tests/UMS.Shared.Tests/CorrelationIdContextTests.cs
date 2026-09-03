using Microsoft.AspNetCore.Http;
using UMS.Shared.Observability.Correlation;

namespace UMS.Shared.Tests;

public class CorrelationIdContextTests
{
    [Fact]
    public void GetOrCreate_reuses_the_incoming_header_value()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CorrelationIdContext.HeaderName] = "existing-correlation-id";

        var correlationId = CorrelationIdContext.GetOrCreate(httpContext);

        Assert.Equal("existing-correlation-id", correlationId);
    }

    [Fact]
    public void GetOrCreate_generates_a_new_id_when_no_header_is_present()
    {
        var httpContext = new DefaultHttpContext();

        var correlationId = CorrelationIdContext.GetOrCreate(httpContext);

        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public void GetOrCreate_returns_the_same_id_across_calls_for_the_same_request()
    {
        var httpContext = new DefaultHttpContext();

        var first = CorrelationIdContext.GetOrCreate(httpContext);
        var second = CorrelationIdContext.GetOrCreate(httpContext);

        Assert.Equal(first, second);
    }
}
