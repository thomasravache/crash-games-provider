namespace Apogeu.Security.Tests.Support;

/// <summary>Completion of a hub invocation: the raw JSON result, or the error the server sent, and the whole message.</summary>
internal sealed record HubCompletion(string? Result, string? Error, string Raw);
