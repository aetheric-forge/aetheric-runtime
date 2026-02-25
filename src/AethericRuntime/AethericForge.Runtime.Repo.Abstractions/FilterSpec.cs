namespace AethericForge.Runtime.Repo.Abstractions;

public interface IFilterSpec
{
    public Guid? Id { get; }
}

public class FilterSpec : IFilterSpec
{
    public Guid? Id { get; init; }
}

