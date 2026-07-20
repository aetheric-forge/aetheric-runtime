namespace AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;

public interface IWorkbenchService
{
    Task PutAsync<TWork>(
        object key,
        TWork work,
        CancellationToken ct = default);

    Task<TWork?> GetAsync<TWork>(
        object key,
        CancellationToken ct = default);

    IDisposable Subscribe<TWork>(
        Func<TWork, CancellationToken, Task> receiver);
}
