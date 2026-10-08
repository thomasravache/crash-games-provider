namespace MediatR;

/// <summary>Stand-in for MediatR's mediator (SPEC-0003 UT-02). Only its name and namespace matter.</summary>
public interface IMediator
{
    Task<object?> Send(object request, CancellationToken cancellationToken = default);
}
