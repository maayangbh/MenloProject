using Microsoft.AspNetCore.Http;

namespace FileService.Api.Infrastructure;

public static class ResultHeaderExtensions
{
    public static IResult WithHeader(this IResult inner, string name, string value)
        => new HeaderResult(inner, name, value);
}

sealed class HeaderResult : IResult
{
    private readonly IResult _inner;
    private readonly string _name;
    private readonly string _value;

    public HeaderResult(IResult inner, string name, string value)
    {
        _inner = inner;
        _name = name;
        _value = value;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        // Add the header first
        httpContext.Response.Headers[_name] = _value;

        // Then execute the real result
        await _inner.ExecuteAsync(httpContext);
    }
}
