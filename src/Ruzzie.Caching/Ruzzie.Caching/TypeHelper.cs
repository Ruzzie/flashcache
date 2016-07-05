using System;
using System.Collections.Generic;
using System.Reflection;

namespace Ruzzie.Caching
{
    internal static class TypeHelper
    {      
        public static int SizeOf<T>(T obj)
        {          
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
        internal static int SizeOf<T>(Type t, T obj = default(T))
#pragma warning restore CS1574 // XML comment has cref attribute that could not be resolved
        {
            int size = 0;
            string typeName = t.FullName;

            if (t.IsArray)
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
                    return SizeInMemoryForArray(sizeOfElement, defaultCollectionSizeWhenDefault, elementType.IsValueType);
                }
               
                if (array == null)
                {                   
                    throw new ArgumentException("Type was not of type Array when determining size but was. " + t.BaseType?.FullName, "obj");
                }

                return SizeInMemoryForArray(sizeOfElement, array.Length, elementType.IsValueType);
            }

            if (t.IsEnum)
            {
                return SizeOf(Enum.GetUnderlyingType(t), obj);
            }

            switch (typeName)
            {

                case "System.SByte":
                    return 1;
                case "System.Byte":
                    return 1;
                case "System.Int16":
                    return 2;
                case "System.UInt16":
                    return 2;
                case "System.Int32":
                    return 4;
                case "System.UInt32":
                    return 4;
                case "System.Int64":
                    return 8;
                case "System.UInt64":
                    return 8;
                case "System.Char":
                    return 2;
                case "System.Single":
                    return 4;
                case "System.Double":
                    return 8;
                case "System.Decimal":
                    return 16;
                case "System.Boolean":
                    return 1;
                case "System.Object":
                    return defaultObjectSizeInBytes;
                case "System.String":
                    return GetStringSize(obj as string);
                case "System.Guid":
                    return 16;
            }

            //no basic types found, decompose fields
            //Ignore properties, since the have backing fields OR are essentially methods
            FieldInfo[] allFieldInfos = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
          
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

                if (fieldInfo.FieldType.IsNested && !fieldInfo.FieldType.IsValueType)//since we cannot resolve 2 way references easily
                {
                    size += IntPtr.Size;
                }
                else
                {
                    if (fieldInfo.FieldType.IsInterface && fieldValue == null)
                    {
              
                    }
                    else
                    {                      
                        int sizeOf = SizeOf(fieldInfo.FieldType, fieldValue);
                        size += sizeOf;
                    }
                }
            }
          
            return SizeHelper.CalculateActualSizeInBytesForType(size, Is64BitProcess, t.IsValueType);
        }

        private static int GetStringSize(string value)
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