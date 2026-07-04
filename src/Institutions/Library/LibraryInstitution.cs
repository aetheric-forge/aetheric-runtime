using System;
using System.Threading.Tasks;

namespace Institutions.Library
{
    public class LibraryInstitution : LibraryBase
    {
        public LibraryInstitution(string name)
        {
            Name = name;
        }

        public override Task CheckOutAsync(int bookId, int userId)
        {
            // TODO: Implement logic
            throw new NotImplementedException();
        }

        public override Task ReturnAsync(int bookId)
        {
            // TODO: Implement logic
            throw new NotImplementedException();
        }
    }
}
