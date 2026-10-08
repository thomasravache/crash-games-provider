using MediatR;

namespace Apogeu.Betting.Features.CashOut;

/// <summary>Known violation (UT-02): depends on MediatR, forbidden by ADR-0002.</summary>
public sealed class CashOutEndpoint(IMediator mediator)
{
    public Task<object?> HandleAsync(object command, CancellationToken cancellationToken) =>
        mediator.Send(command, cancellationToken);
}
