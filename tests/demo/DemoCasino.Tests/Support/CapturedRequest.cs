namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>A request as it left the casino's HTTP pipeline (see <see cref="CapturingHandler"/>).</summary>
internal sealed record CapturedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, byte[] Body);
