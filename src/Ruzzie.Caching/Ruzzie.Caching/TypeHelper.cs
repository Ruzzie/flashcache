using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Ruzzie.Caching
{
    internal static class TypeHelper
    {      
        public static int SizeOf<T>(in T obj)
        {          
            var underlyingTypeForNullable =  Nullable.GetUnderlyingType(typeof(T));
            if (underlyingTypeForNullable != null)
            {
                return SizeOf(typeof(T), obj);
            }
            return SizeOf(obj.GetType(),obj);
        }

        private static readonly int defaultCollectionSizeWhenDefault = 89;
        private static readonly int defaultObjectSizeInBytes = 32;//24;//32
        private static readonly int defaultStringLength = 20;

#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved
        /// <exception cref="TargetException">In the .NET for Windows Store apps or the Portable Class Library, catch <see cref="T:System.Exception" /> instead.The field is non-static and <paramref name="obj" /> is null. </exception>
        /// <exception cref="FieldAccessException">In the .NET for Windows Store apps or the Portable Class Library, catch the base class exception, <see cref="T:System.MemberAccessException" />, instead.The caller does not have permission to access this field. </exception>
        /// <exception cref="NotSupportedException">A field is marked literal, but the field does not have one of the accepted literal types. </exception>
        /// <exception cref="ArgumentException">The method is neither declared nor inherited by the class of <paramref name="obj" />. </exception>
        internal static int SizeOf<T>(Type t, in T obj = default(T))
#pragma warning restore CS1574 // XML comment has cref attribute that could not be resolved
        {
            int size = 0;
            string typeName = t.FullName;

            if (t.IsArray)
            {
                return SizeOfArray(t, obj);
            }

            if (t.IsEnum())
            {
                return SizeOf(Enum.GetUnderlyingType(t), obj);
            }
            
            var underlyingTypeForNullable =  Nullable.GetUnderlyingType(t);
            if (underlyingTypeForNullable != null)
            {
                return SizeOf<bool>(typeof(bool)) + SizeOf(underlyingTypeForNullable, obj);
            }
            
            if (IsSimpleType(obj, typeName, out size))
            {
                return size;
            }

            size = SizeOfCustomType(t, obj, size);

            return SizeHelper.CalculateActualSizeInBytesForType(size, Is64BitProcess, t.IsValueType());
        }

        [SuppressMessage("Microsoft.Maintainability","CA1502:AvoidExcessiveComplexity")]
        private static bool IsSimpleType<T>(in T obj, in string typeName, out int size)
        {
            switch (typeName)
            {
                case "System.SByte":
                {
                    size = 1;
                    return true;
                }
                case "System.Byte":
                {
                    size = 1;
                    return true;
                }
                case "System.Int16":
                {
                    size = 2;
                    return true;
                }
                case "System.UInt16":
                {
                    size = 2;
                    return true;
                }
                case "System.Int32":
                {
                    size = 4;
                    return true;
                }
                case "System.UInt32":
                {
                    size = 4;
                    return true;
                }
                case "System.Int64":
                {
                    size = 8;
                    return true;
                }
                case "System.UInt64":
                {
                    size = 8;
                    return true;
                }
                case "System.Char":
                {
                    size = 2;
                    return true;
                }
                case "System.Single":
                {
                    size = 4;
                    return true;
                }
                case "System.Double":
                {
                    size = 8;
                    return true;
                }
                case "System.Decimal":
                {
                    size = 16;
                    return true;
                }
                case "System.Boolean":
                {
                    size = 1;
                    return true;
                }
                case "System.Object":
                {
                    size = defaultObjectSizeInBytes;
                    return true;
                }
                case "System.String":
                {
                    size = GetStringSize(obj as string);
                    return true;
                }
                case "System.Guid":
                {
                    size = 16;
                    return true;
                }
            }           

            size = 0;
            return false;
        }

        private static int SizeOfCustomType<T>(Type t, in T obj, int size)
        {
#if HAVE_FULL_REFLECTION
            //no basic types found, decompose fields
            //Ignore properties, since the have backing fields OR are essentially methods
            FieldInfo[] allFieldInfos = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.DeclaredOnly);
#else
            FieldInfo[] allFieldInfos =
 t.GetRuntimeFields().Where(fi => /*fi.IsPublic &&*/ fi.IsStatic == false).ToArray();
#endif
            for (int i = 0; i < allFieldInfos.Length; i++)
            {
                FieldInfo fieldInfo = allFieldInfos[i];
                object fieldValue;

                if (obj == null)
                {
                    fieldValue = null;
                }
                else
                {
                    fieldValue = fieldInfo.GetValue(obj);
                }

                if (fieldInfo.FieldType.IsNested && !fieldInfo.FieldType.IsValueType()
                ) //since we cannot resolve 2 way references easily
                {
                    size += IntPtr.Size;
                }
                else
                {
                    if (fieldInfo.FieldType.IsInterface() && fieldValue == null)
                    {
                    }
                    else
                    {
                        int sizeOf = SizeOf(fieldInfo.FieldType, fieldValue);
                        size += sizeOf;
                    }
                }
            }

            return size;
        }

        private static int SizeOfArray<T>(Type t, in T obj)
        {
            Array array = obj as Array;
            Type elementType = t.GetElementType();

            object elementObject = null;
            if (array != null && array.Length > 0)
            {
                elementObject = array.GetValue(0);
            }

            int sizeOfElement = SizeOf(elementType, elementObject);

            if (EqualityComparer<T>.Default.Equals(obj) || obj == null)
            {
                return SizeInMemoryForArray(sizeOfElement, defaultCollectionSizeWhenDefault, elementType.IsValueType());
            }

            if (array == null)
            {
                throw new ArgumentException(
                    "Type was not of type Array when determining size but was. " + t.BaseType()?.FullName, "obj");
            }

            return SizeInMemoryForArray(sizeOfElement, array.Length, elementType.IsValueType());
        }

        private static int GetStringSize(in string value)
        {
            int stringSize = defaultStringLength;
            if (!string.IsNullOrWhiteSpace(value))
            {
                stringSize = value.Length;
            }

            return (StringOverHead() +  ((stringSize+1) * 2).RoundUpToNearest(IntPtr.Size));         
        }

        private static int StringOverHead()
        {
            if (Is64BitProcess)
            {
                return 32;
            }
            return 20;
        }

        private static int ArrayOverHeadValueType() //approximate
        {
            if (Is64BitProcess)
            {
                return 28;
            }

            return 12;
        }

        internal static int ArrayOverHeadReferenceType() //approximate
        {
            if (Is64BitProcess)
            {
                return 32;
            }

            return 16;
        }

        internal static int TypeOverhead(bool is64BitProcess)//approximate
        {
            if (is64BitProcess)
            {
                return 24;
            }
            return 12;
        }

        //beware this is an estimation
        private static int SizeInMemoryForArray(int sizeOfElement, int numberOfElements, bool isValueType)
        {
            int overhead = isValueType ? ArrayOverHeadValueType() : ArrayOverHeadReferenceType();
            return (overhead + (sizeOfElement * (numberOfElements)));
        }

        static readonly bool Is64BitProcess = (IntPtr.Size == 8);        
    }
}