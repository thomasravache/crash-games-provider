using Apogeu.Operators.Infrastructure.Catalog;

namespace Apogeu.Operators.Features.Catalog;

/// <summary>
/// A view of the <see cref="ICatalogStore"/> kept in memory while the store <see cref="ICatalogStore.Version"/> does not
/// change (SPEC-0020 §5): a read costs one version check; a new version loads the store once and rebuilds the view.
/// </summary>
internal sealed class CatalogCache<TView>(ICatalogStore store, Func<CatalogSnapshot, TView> build)
    where TView : class
{
    private volatile Entry? _current;

    public TView Current
    {
        get
        {
            var version = store.Version;
            var current = _current;
            if (current is null || current.Version != version)
            {
                // Concurrent readers may both reload: the view is immutable, so the last one simply wins.
                var snapshot = store.Load();
                current = new Entry(snapshot.Version, build(snapshot));
                _current = current;
            }

            return current.View;
        }
    }

    private sealed record Entry(long Version, TView View);
}
