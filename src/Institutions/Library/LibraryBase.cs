using System.Threading.Tasks;

namespace Institutions.Library
{
    public abstract class LibraryBase : ILibrary
    {
        public virtual string Name { get; protected set; }

        public abstract Task CheckOutAsync(int bookId, int userId);
        public abstract Task ReturnAsync(int bookId);
        // Base logic can go here if needed
    }
}
