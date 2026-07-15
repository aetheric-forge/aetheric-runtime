using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace AethericForge.Runtime.Institutions.Abstractions.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInstitutionTemplate(
        this IServiceCollection services, 
        Action<InstitutionTemplateBuilder> configure)
    {
        var builder = InstitutionTemplateBuilder.Create();
        configure(builder);
        var template = builder.Build();
        
        services.AddSingleton(template);
        return services;
    }
}