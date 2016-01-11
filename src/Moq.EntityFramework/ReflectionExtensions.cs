using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moq.EntityFramework
{
    internal static class ReflectionExtensions
    {
        public static bool Is<T>(this Type type)
        {
            return Is(type, typeof(T));
        }

        public static bool ImplementsGenericTypeDefinition(this Type toCheck, Type genericType)
        {
            if (!genericType.IsGenericTypeDefinition)
                throw new ArgumentException("Specified type is not a generic type definition", nameof(genericType));

            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (genericType == cur)
                {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }

        public static bool Is(this Type type, Type compareType)
        {
            if (compareType.IsGenericTypeDefinition)
                return type.ImplementsGenericTypeDefinition(compareType);
            return compareType.IsAssignableFrom(type);
        }

        public static bool IsNullable(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}
