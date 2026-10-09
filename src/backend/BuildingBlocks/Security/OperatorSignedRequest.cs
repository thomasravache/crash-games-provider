namespace Apogeu.BuildingBlocks.Security;

/// <summary>The parts of an operator request that the signature covers, plus the signature it carries.</summary>
/// <param name="Timestamp">Value of <see cref="OperatorSignatureHeaders.Timestamp"/> (Unix seconds).</param>
/// <param name="Method">HTTP method.</param>
/// <param name="Path">Request path, without the query string.</param>
/// <param name="Body">Raw request body.</param>
/// <param name="Signature">Value of <see cref="OperatorSignatureHeaders.Signature"/>.</param>
public sealed record OperatorSignedRequest(long Timestamp, string Method, string Path, ReadOnlyMemory<byte> Body, string Signature);
