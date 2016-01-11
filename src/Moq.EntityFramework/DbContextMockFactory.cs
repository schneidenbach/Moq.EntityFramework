using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Moq.EntityFramework
{
    /// <summary>
    /// Provides factory method for creating DbContextMocks.
    /// </summary>
    public static class DbContextMockFactory
    {
        /// <summary>
        /// Creates a DbContextMock with the specified DbContext type.
        /// </summary>
        /// <typeparam name="T">The type of the DbContext you want to create a mock for.</typeparam>
        /// <returns></returns>
        public static DbContextMock<T> Create<T>()
            where T : DbContext
        {
            return new DbContextMock<T>();
        }
    }
}
