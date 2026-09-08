namespace AethericForge.Runtime.Abstractions.Interfaces.TableTop.Primitives;

/// <summary>An opaque external address scoped to a provider and its workspace/account.</summary>
public interface ITableTopReference
{
    string Provider { get; }
    string Scope { get; }
    string Id { get; }
}
