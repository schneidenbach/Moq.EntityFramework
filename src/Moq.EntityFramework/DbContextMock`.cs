using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Moq.Language;

namespace Moq.EntityFramework
{
    /// <summary>
    /// Represents a mocked DbContext.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <seealso cref="Moq.Mock{TContext}" />
    public class DbContextMock<TContext> : Mock<TContext>
         where TContext : DbContext
    {
        internal Dictionary<Type, IList> DbSetCollections { get; } = new Dictionary<Type, IList>();
        internal Dictionary<Type, object> DbSetMocks { get; } = new Dictionary<Type, object>();
        internal MethodInfo SetupMethodGeneric { get; }
        internal Dictionary<Type, PropertyInfo> DbSetProperties { get; }

        internal DbContextMock()
        {
            DbSetProperties = GetDbSetProperties();
            SetupMethodGeneric = GetGenericSetupMethod();
            SetupDbSetCollections();
        }

        private MethodInfo GetGenericSetupMethod()
        {
            return GetType().GetMethods()
                .Single(
                    m => {
                        var parameters = m.GetParameters();

                        return m.Name == "Setup" &&
                               parameters.Length == 1 &&
                               parameters[0].ParameterType.GenericTypeArguments[0].Is(typeof(Func<,>));
                    });
        }

        private static Dictionary<Type, PropertyInfo> GetDbSetProperties()
        {
            return typeof(TContext).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.Is(typeof(DbSet<>)))
                .ToDictionary(p => p.PropertyType.GenericTypeArguments[0], p => p);
        }

        /// <summary>
        /// Mocks all sets in a mocked DbContext.
        /// </summary>
        /// <returns>Returns the MockDbContext.</returns>
        /// <remarks>For larger DbContexts, consider mocking each set individually, as using this method might significantly slow down your unit tests.</remarks>
        public DbContextMock<TContext> MockAllSets()
        {
            foreach (var prop in DbSetProperties.Values)
                MockDbSet(prop);
            return this;
        }

        /// <summary>
        /// Mocks the set for a given type.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity whose set should be mocked. If the set has already been mocked, returns the previously mocked set.</typeparam>
        /// <param name="entities">The entities you would like added to the DbSet.</param>
        /// <returns>Returns the MockDbContext.</returns>
        public DbContextMock<TContext> MockSetFor<TEntity>(params TEntity[] entities)
            where TEntity : class
        {
            var entityType = typeof(TEntity);
            EnsureEntityExistsAsSet(entityType);

            MockDbSetForEntity(entityType);
            foreach (var e in entities) 
                DbSetCollections[entityType].Add(e);
            return this;
        }

        /// <summary>
        /// Gets the mocked set for a given entity type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public Mock<DbSet<T>> MockedSet<T>() where T : class
        {
            var type = typeof(T);
            EnsureEntityExistsAsSet(type);

            if (!DbSetProperties.ContainsKey(type))
                throw new ArgumentException($"Type {type.Name} is not in a DbSet in this context.");

            if (!DbSetMocks.ContainsKey(type))
                MockDbSetForEntity(type);

            return (Mock<DbSet<T>>)DbSetMocks[type];
        }

        private void EnsureEntityExistsAsSet(Type type)
        {
            if (!DbSetCollections.ContainsKey(type))
                throw new InvalidOperationException($"No set is defined in this context with type '{type}'");
        }

        private void SetupDbSetCollections()
        {
            foreach (var propPair in DbSetProperties)
            {
                var prop = propPair.Value;
                var listType = typeof(List<>).MakeGenericType(prop.PropertyType.GenericTypeArguments[0]);
                var list = Activator.CreateInstance(listType);

                DbSetCollections[propPair.Key] = (IList) list;
            }
        }

        private void MockDbSetForEntity(Type entityType)
        {
            MockDbSet(DbSetProperties[entityType]);
        }

        private void MockDbSet(PropertyInfo dbSetProperty)
        {
            var dbSetType = dbSetProperty.PropertyType;
            var dbSetEntityType = dbSetType.GenericTypeArguments[0];

            if (DbSetMocks.ContainsKey(dbSetEntityType))
                return;

            var mockType = typeof(Mock<>);

            var mockSetType = mockType.MakeGenericType(dbSetType);
            var mockSet = Activator.CreateInstance(mockSetType);
            var mockSetData = DbSetCollections[dbSetEntityType];
            var mockDataSetQueryable = mockSetData.AsQueryable();

            SetupQueryableMethods(mockSetType, mockDataSetQueryable, mockSet, dbSetEntityType);
            CallMoqSetup(dbSetProperty, dbSetType, mockSet);

            DbSetMocks[dbSetEntityType] = mockSet;
        }

        private void CallMoqSetup(PropertyInfo dbSetProperty, Type dbSetType, object mockSet)
        {
            var param = Expression.Parameter(typeof (TContext));
            var selectorType = typeof (Func<,>).MakeGenericType(typeof (TContext), dbSetType);
            var exp = Expression.Lambda(
                selectorType,
                Expression.Property(param, dbSetProperty),
                param
                );

            var genericSetupMethod = SetupMethodGeneric.MakeGenericMethod(dbSetType);
            var theSetup = genericSetupMethod.Invoke(this, new object[] {exp});

            var mockSetExpression = Expression.Convert(Expression.Constant(mockSet), typeof (Mock));
            var objectExpression = Expression.Convert(
                Expression.Property(mockSetExpression, typeof (Mock), nameof(Mock.Object)), dbSetType);
            Expression.Lambda(Expression.Call(Expression.Constant(theSetup), nameof(IReturns<object, object>.Returns),
                new Type[] {}, new Expression[] {objectExpression})).Compile().DynamicInvoke();
        }

        private static void SetupQueryableMethods(Type mockSetType, IQueryable mockDataSetQueryable, object mockSet,
            Type dbSetEntityType)
        {
            var providerType = typeof (TestDbAsyncQueryProvider<>).MakeGenericType(mockSetType);
            var provider = (IQueryProvider) Activator.CreateInstance(providerType, mockDataSetQueryable.Provider);

            SetupIQueryableReturn(mockSet, mockDataSetQueryable, q => q.Expression);
            SetupIQueryableReturn(mockSet, mockDataSetQueryable, q => q.Provider, provider);
            SetupIQueryableReturn(mockSet, mockDataSetQueryable, q => q.ElementType);
            SetupIQueryableReturn(mockSet, mockDataSetQueryable, q => q.GetEnumerator());
            SetupIDbAsyncEnumerableReturn(dbSetEntityType, mockDataSetQueryable, mockSet);
        }

        private static void SetupIDbAsyncEnumerableReturn(Type dbSetEntityType, IQueryable mockSetData, object mockSet)
        {
            var testDbAsyncEnumeratorType = typeof(TestDbAsyncEnumerator<>).MakeGenericType(dbSetEntityType);

            var iEnumerableType = typeof(IEnumerable<>).MakeGenericType(dbSetEntityType);
            var enumerator = iEnumerableType.GetMethod(nameof(IEnumerable.GetEnumerator), new Type[] { });

            var testDbAsyncEnumerator = Activator.CreateInstance(testDbAsyncEnumeratorType, enumerator.Invoke(mockSetData, null));

            var iDbAsyncEnumerableType = typeof(IDbAsyncEnumerable<>).MakeGenericType(dbSetEntityType);
            var functionType = typeof(Func<,>).MakeGenericType(iDbAsyncEnumerableType,
                typeof(IDbAsyncEnumerator<>).MakeGenericType(dbSetEntityType));

            var parameter = Expression.Parameter(iDbAsyncEnumerableType);
            var expression = Expression.Lambda(functionType, Expression.Call(parameter, nameof(IDbAsyncEnumerable.GetAsyncEnumerator), new Type[] { }),
                parameter);

            SetupReturn(mockSet, mockSetData, expression, testDbAsyncEnumerator);
        }

        private static void SetupIQueryableReturn<TReturnType>(object mockSet, IQueryable mockSetData, Expression<Func<IQueryable, TReturnType>> asExpression, object returns = null)
            where TReturnType : class
        {
            SetupReturn(mockSet, mockSetData, asExpression, returns);
        }

        private static void SetupReturn(object mockSet, IQueryable mockSetData, LambdaExpression asExpression, object returns = null)
        {
            var mockSetExpression = Expression.Constant(mockSet);
            var returnType = asExpression.ReturnType;

            var asCallExpression = Expression.Call(mockSetExpression, nameof(As), new[] { asExpression.Type.GenericTypeArguments[0] });
            var setupCallExpression = Expression.Call(asCallExpression, nameof(Setup), new[] { returnType }, asExpression);
            var value = returns ?? asExpression.Compile().DynamicInvoke(mockSetData);
            var valueExpression = Expression.Constant(value, returnType);

            var setupTypeArguments = setupCallExpression.Type.GenericTypeArguments;
            var iReturnsType = typeof(IReturns<,>).MakeGenericType(setupTypeArguments[0], setupTypeArguments[1]);
            var returnsCallExpression = Expression.Call(Expression.Convert(setupCallExpression, iReturnsType), nameof(IReturns<object, object>.Returns), new Type[] { }, valueExpression);

            Expression.Lambda(Expression.Block(asCallExpression, setupCallExpression, returnsCallExpression)).Compile().DynamicInvoke();
        }
    }
}

