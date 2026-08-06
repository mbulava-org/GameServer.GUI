namespace GameServer.Docker.Services.V2.MountTypeHandlers;

/// <summary>
/// Resolves the correct <see cref="IMountTypeHandler"/> for a given mount-type code.
/// </summary>
public interface IMountTypeHandlerFactory
{
    /// <summary>
    /// Returns the handler registered for <paramref name="mountType"/> (case-insensitive).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no handler is registered for the requested mount type.
    /// </exception>
    IMountTypeHandler GetHandler(string mountType);
}

/// <summary>
/// Default <see cref="IMountTypeHandlerFactory"/> that indexes all registered
/// <see cref="IMountTypeHandler"/> instances by their <see cref="IMountTypeHandler.MountTypeKey"/>.
/// </summary>
public sealed class MountTypeHandlerFactory : IMountTypeHandlerFactory
{
    private readonly IReadOnlyDictionary<string, IMountTypeHandler> _handlers;

    public MountTypeHandlerFactory(IEnumerable<IMountTypeHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var map = new Dictionary<string, IMountTypeHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var handler in handlers)
        {
            if (string.IsNullOrWhiteSpace(handler.MountTypeKey))
            {
                throw new InvalidOperationException(
                    $"Mount type handler '{handler.GetType().Name}' has an empty MountTypeKey.");
            }

            if (!map.TryAdd(handler.MountTypeKey, handler))
            {
                throw new InvalidOperationException(
                    $"Duplicate mount type handler registered for key '{handler.MountTypeKey}'.");
            }
        }

        _handlers = map;
    }

    public IMountTypeHandler GetHandler(string mountType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mountType);

        return _handlers.TryGetValue(mountType, out var handler)
            ? handler
            : throw new InvalidOperationException(
                $"No mount type handler is registered for mount type '{mountType}'.");
    }
}
