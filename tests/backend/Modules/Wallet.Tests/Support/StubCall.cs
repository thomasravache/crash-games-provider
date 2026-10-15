namespace Apogeu.Wallet.Tests.Support;

/// <summary>A request and the answer the operator test server gave: HTTP status and, on 200, the <c>status</c> of the body.</summary>
internal sealed record StubCall(StubRequest Request, int StatusCode, string? Status);
