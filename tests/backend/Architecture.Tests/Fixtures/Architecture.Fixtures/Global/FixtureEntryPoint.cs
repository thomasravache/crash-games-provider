using MediatR;

#pragma warning disable CA1050, MA0047 // Fixture (UT-02): a global-namespace type, like the one generated for top-level statements.

/// <summary>Known violation (UT-02): a global-namespace type (top-level Program style) using MediatR (ADR-0002).</summary>
public static class FixtureEntryPoint
{
    public static Task<object?> RunAsync(IMediator mediator, object request) => mediator.Send(request);
}
