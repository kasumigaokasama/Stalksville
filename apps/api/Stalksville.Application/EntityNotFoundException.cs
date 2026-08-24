namespace Stalksville.Application;

/// <summary>Thrown when a locally tracked entity does not exist; maps to HTTP 404.</summary>
public sealed class EntityNotFoundException(string entityType, object key)
    : Exception($"No {entityType} found for key '{key}'.")
{
    public string EntityType { get; } = entityType;

    public object Key { get; } = key;
}
