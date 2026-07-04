using Abstractions;
using System.Threading.Tasks;

namespace Institutions.Library
{
    public interface ILibraryMethods : IInstitutionMethods
    {
        Task CheckOutAsync(int bookId, int userId);
        Task ReturnAsync(int bookId);
        // ... add more domain methods as needed
    }
}
