using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Models.Knowledge.Core;

public abstract class KnowledgeObjectBase : IKnowledgeObject
{
    protected KnowledgeObjectBase(
        IKnowledgeReference reference,
        IKnowledgeDescriptor descriptor,
        KnowledgeLifecycle lifecycle,
        KnowledgeState state,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Lifecycle = ValidateDefined(lifecycle, nameof(lifecycle));
        State = ValidateDefined(state, nameof(state));
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();

        if (UpdatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(updatedAtUtc),
                updatedAtUtc,
                "UpdatedAtUtc must be greater than or equal to CreatedAtUtc.");
        }
    }

    protected KnowledgeObjectBase(
        IKnowledgeReference reference,
        IKnowledgeDescriptor descriptor)
        : this(reference, descriptor, DateTimeOffset.UtcNow)
    {
    }

    private KnowledgeObjectBase(
        IKnowledgeReference reference,
        IKnowledgeDescriptor descriptor,
        DateTimeOffset timestampUtc)
        : this(
            reference,
            descriptor,
            KnowledgeLifecycle.Catalogued,
            KnowledgeState.Available,
            timestampUtc,
            timestampUtc)
    {
    }

    public IKnowledgeReference Reference { get; }
    public IKnowledgeDescriptor Descriptor { get; }
    public KnowledgeLifecycle Lifecycle { get; }
    public KnowledgeState State { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; }

    private static TEnum ValidateDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be a defined enum value.");
        }

        return value;
    }
}
