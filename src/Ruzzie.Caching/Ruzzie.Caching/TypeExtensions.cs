using System;

namespace Ruzzie.Caching
{
    internal static class TypeExtensions
    {
        public static bool IsGenericType(this Type type)
        {
            return type.IsGenericType;
        }

        public static bool IsGenericTypeDefinition(this Type type)
        {
            return type.IsGenericTypeDefinition;

        }

        public static bool IsInterface(this Type type)
        {
            return type.IsInterface;
        }
      
    }
}
