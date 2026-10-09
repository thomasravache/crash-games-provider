using Apogeu.Operators.Contracts;

namespace Apogeu.Host.Security;

/// <summary>
/// Security headers of every response of the Host (SPEC-0009 §5): <c>X-Content-Type-Options: nosniff</c>,
/// <c>Referrer-Policy: no-referrer</c> and a <c>Content-Security-Policy</c> that allows no content and whose
/// <c>frame-ancestors</c> lists the origins the operators configured (<c>'none'</c> when there is none). HSTS comes from
/// <c>UseHsts</c> (HTTPS on a host other than localhost).
/// </summary>
internal sealed class SecurityHeaders(RequestDelegate next, IOperatorFrameAncestors frameAncestors)
{
    private readonly Lazy<string> _contentSecurityPolicy = new(() =>
        "default-src 'none'; frame-ancestors "
        + (frameAncestors.Origins.Count == 0 ? "'none'" : string.Join(' ', frameAncestors.Origins)));

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(
            static state =>
            {
                var (response, policy) = ((HttpResponse, string))state;
                response.Headers.XContentTypeOptions = "nosniff";
                response.Headers["Referrer-Policy"] = "no-referrer";
                response.Headers.ContentSecurityPolicy = policy;
                return Task.CompletedTask;
            },
            (context.Response, _contentSecurityPolicy.Value));
        return next(context);
    }
}
