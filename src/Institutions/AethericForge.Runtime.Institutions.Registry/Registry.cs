using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Registry;

public sealed class Registry(IRegistryContext context, IRegistryService registryService, IRegistrar registrar) 
    : InstitutionBase(context), IRegistry
{
    private readonly IRegistryService _registryService = 
        registryService ?? throw new ArgumentNullException(nameof(registryService));

    public new IRegistryContext Context => (IRegistryContext)base.Context;
    
    public IRegistrar Registrar { get; } = registrar ?? throw new ArgumentNullException(nameof(registrar));
}
