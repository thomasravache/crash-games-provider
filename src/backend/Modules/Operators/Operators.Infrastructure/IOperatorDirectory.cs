using Apogeu.Operators.Infrastructure.Persistence;

namespace Apogeu.Operators.Infrastructure;

/// <summary>The registered operators and the sala each one is associated with.</summary>
public interface IOperatorDirectory
{
    /// <summary>The operator <paramref name="operatorId"/>, or <see langword="null"/> when it is not registered.</summary>
    Task<OperatorRecord?> FindAsync(string operatorId, CancellationToken cancellationToken);

    /// <summary>Registers the operator, or updates its name and sala when it already exists (idempotent).</summary>
    Task EnsureAsync(OperatorRecord record, CancellationToken cancellationToken);
}
