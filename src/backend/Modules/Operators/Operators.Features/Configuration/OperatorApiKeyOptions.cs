namespace Apogeu.Operators.Features.Configuration;

/// <summary>One operator API key: <c>Operators:Demo:ApiKeys:{n}:Id</c> and <c>:Secret</c>.</summary>
public sealed class OperatorApiKeyOptions
{
    public string? Id { get; set; }

    public string? Secret { get; set; }
}
