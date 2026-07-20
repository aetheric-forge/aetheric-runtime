using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Archive;
using AethericForge.Runtime.Institutions.Library;
using AethericForge.Runtime.Institutions.PostOffice;
using AethericForge.Runtime.Institutions.Registry;
using AethericForge.Runtime.Institutions.Workbench;

namespace AethericForge.Runtime.Institutions.Campus;

public interface ICampus : IInstitution
{
    IArchive Archive { get; }

    IPostOffice PostOffice { get; }

    IRegistry Registry { get; }
    
    ILibrary Library { get; }
    
    IWorkbench Workbench { get; }
}