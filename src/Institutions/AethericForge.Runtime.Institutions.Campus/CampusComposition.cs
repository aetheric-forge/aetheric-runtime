using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Faculty.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Institutions.Abstractions.Models;
using AethericForge.Runtime.Institutions.Faculty;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Services.Faculty;
using Microsoft.Extensions.DependencyInjection;

namespace AethericForge.Runtime.Institutions.Campus;

/// <summary>
/// Every institution context in this runtime (WorkbenchContext, LibraryContext, RegistryContext,
/// ArchiveContext, PostOfficeContext, ...) shares the same (IInstitutionTemplate, IServiceProvider,
/// IInstitution? parent) constructor shape - this generic helper replaces the
/// "derive a per-institution template, construct its context, ActivatorUtilities.CreateInstance,
/// campus.Register" boilerplate that was independently hand-repeated per institution in every app's
/// own ForgeCampusExtensions.cs.
/// </summary>
public static class CampusCompositionExtensions
{
    public static TInterface RegisterInstitution<TInterface, TImplementation, TContext>(
        this Campus campus,
        InstitutionTemplate campusTemplate,
        IServiceProvider serviceProvider,
        string name,
        Func<InstitutionTemplate, IServiceProvider, IInstitution, TContext> createContext)
        where TInterface : class, IInstitution
        where TImplementation : class, TInterface
        where TContext : IInstitutionContext
    {
        var template = campusTemplate with
        {
            Descriptor = new InstitutionDescriptor(name, campusTemplate.Descriptor.Version, $"{name} institution")
        };
        var context = createContext(template, serviceProvider, campus);
        var institution = ActivatorUtilities.CreateInstance<TImplementation>(serviceProvider, context!);
        campus.Register<TInterface>(institution);
        return institution;
    }

    /// <summary>
    /// A Faculty groups other Institutions under a single Dean (see IFaculty) - it owns no external
    /// resource, so unlike RegisterInstitution above it takes a factory producing the concrete
    /// Faculty from its context and Dean directly, rather than relying on ActivatorUtilities to
    /// resolve constructor parameters from DI.
    /// </summary>
    public static TFaculty RegisterFaculty<TFaculty>(
        this Campus campus,
        InstitutionTemplate campusTemplate,
        IServiceProvider serviceProvider,
        string name,
        string deanTitle,
        Func<IFacultyContext, IDean, TFaculty> factory)
        where TFaculty : class, IFaculty
    {
        var template = campusTemplate with
        {
            Descriptor = new InstitutionDescriptor(name, campusTemplate.Descriptor.Version, $"{name} faculty")
        };
        var context = new FacultyContext(template, serviceProvider, campus);
        var dean = new Dean(deanTitle, new Team<IFacultyClerk>(Array.Empty<IFacultyClerk>()));
        var faculty = factory(context, dean);
        campus.Register<TFaculty>(faculty);
        return faculty;
    }
}
