using System;
using System.Collections.Generic;
using Abstractions;

namespace Campus
{
    public class Campus
    {
        private readonly IDictionary<Type, IInstitution> _institutions = new Dictionary<Type, IInstitution>();

        public void RegisterInstitution<T>(T institution) where T : IInstitution
        {
            _institutions[typeof(T)] = institution;
        }

        public T GetInstitution<T>() where T : IInstitution
        {
            return (T)_institutions[typeof(T)];
        }
    }
}
