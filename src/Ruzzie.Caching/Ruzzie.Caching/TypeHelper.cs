using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

#if !PORTABLE
using System.Collections.Concurrent;
using System.Reflection.Emit;
#endif

namespace Ruzzie.Caching
{
    internal static class TypeHelper
    {
        //public static int SizeOf<T>(T? obj) where T : struct
        //{
        //    if (obj == null)
        //    {
        //        throw new ArgumentNullException("obj");
        //    }          
        //    return SizeOf(obj.GetType());
          
        //}

        public static int SizeOf<T>(T obj)
        {          
            return SizeOf(obj.GetType(),obj);
        }

        static readonly int defaultCollectionSizeWhenDefault = 89;
        private static readonly int defaultObjectSizeInBytes = 32;//24;//32
        private static readonly int defaultStringLength = 20;

        internal static int SizeOf<T>(Type t, T obj = default(T))
        {

            int size = 0;
            string typeName = t.FullName;

            //Debug.WriteLine(typeName);

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
                    // ReSharper disable once PossibleNullReferenceException
                    throw new ArgumentException("Type was not of type Array when determining size but was. " + t.BaseType.FullName, "obj");
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

            //Type keyType;
            //Type valueType;
            //if (IsDictionary(t, obj, out keyType, out valueType))
            //{

            //    int collectionSize = GetCollectionSizeOrDefault(obj as ICollection);

            //    size = (SizeOf(keyType, obj) + SizeOf(valueType, obj)) * collectionSize;
            //}

            //no basic types found, decompose fields
            FieldInfo[] fieldInfos = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
          
            for (int i = 0; i < fieldInfos.Length; i++)
            {
                FieldInfo fieldInfo = fieldInfos[i];
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
                    //Debug.WriteLine("Nested ReferenceType " + fieldInfo.Name);
                    size += IntPtr.Size; //TypeOverhead();
                }
                else
                {
                    if (fieldInfo.FieldType.IsInterface && fieldValue == null)
                    {
                        //Debug.WriteLine("IsIntf " + fieldInfo.Name + " == null DeclaringType: " + fieldInfo.DeclaringType);
                        //fieldInfo.FieldType.DeclaringType
                    }
                    else
                    {
                        //if (fieldInfo.FieldType.IsValueType)
                        //{
                        //    Debug.WriteLine("ValType " + fieldInfo.Name);
                        //}
                        //if (fieldInfo.FieldType.IsInterface)
                        //{
                        //    Debug.WriteLine("Interface " + fieldInfo.Name);
                        //}

                        int sizeOf = SizeOf(fieldInfo.FieldType, fieldValue);
                        //Debug.WriteLine(fieldInfo.Name + " SizeOf:" + sizeOf);
                        size += sizeOf;
                    }
                }
            }
          
            //return TypeOverhead() + SizeHelpers.RoundUpToNearest(size, IntPtr.Size); 
            //return SizeHelpers.RoundUpToNearest(size + TypeOverhead(), IntPtr.Size);
            return SizeHelpers.ActualSize(size, Is64BitProcess, t.IsValueType);
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

        static int StringOverHead()
        {
            if (Is64BitProcess)
            {
                return 32;
            }
            return 20;
        }

        static int ArrayOverHeadValueType() //aproximate
        {
            if (Is64BitProcess)
            {
                return 28;
            }

            return 12;
        }

        internal static int ArrayOverHeadReferenceType() //aproximate
        {
            if (Is64BitProcess)
            {
                return 32;
            }

            return 16;
        }

        internal static int TypeOverhead()//aproximate
        {
           return TypeOverhead(Is64BitProcess);
        }

        internal static int TypeOverhead(bool is64BitProcess)//aproximate
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

        private static int GetCollectionSizeOrDefault(ICollection collection)
        {
            if (collection == null || collection.Count == 0)
            {
                return defaultCollectionSizeWhenDefault;
            }
            return collection.Count;
        }

        private static bool IsDictionary(Type type, object obj, out Type keyType, out Type valueType)
        {
            Type genericDictionaryType;
            if (ImplementsGenericDefinition(type, typeof (IDictionary<,>), out genericDictionaryType))
            {
                if (genericDictionaryType.IsGenericTypeDefinition())
                {
                    throw new Exception(string.Format(CultureInfo.InvariantCulture,"Type {0} is not a dictionary.",type));
                }

                Type[] dictionaryGenericArguments = genericDictionaryType.GetGenericArguments();

                keyType = dictionaryGenericArguments[0];
                valueType = dictionaryGenericArguments[1];
                return true;
            }

            keyType = null;
            valueType = null;

            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                //TODO: some magic
                IDictionary dict = obj as IDictionary;
                if (dict != null && dict.Count > 0)
                {
                    IDictionaryEnumerator dictionaryEnumerator = dict.GetEnumerator();
                    dictionaryEnumerator.MoveNext(); //first

                    if (dictionaryEnumerator.Key != null)
                    {
                        keyType = dictionaryEnumerator.Key.GetType();
                        valueType = dictionaryEnumerator.Value.GetType();
                    }                   
                }
                else
                {
                    //return object types
                    keyType = typeof(object);
                    valueType = typeof(object);
                }

                return true;
            }
            return false;

            //return typeof (IDictionary<,>).IsAssignableFrom(type);
            //return type.IsGenericType && type.GetGenericTypeDefinition() == typeof (IDictionary<,>);
        }

        private static bool IsArray(Type type)
        {
            return type.IsArray;
        }

        public static bool ImplementsGenericDefinition(Type type, Type genericInterfaceDefinition)
        {
            Type implementingType;
            return ImplementsGenericDefinition(type, genericInterfaceDefinition, out implementingType);
        }

        public static bool ImplementsGenericDefinition(Type type, Type genericInterfaceDefinition, out Type implementingType)
        {
            //ValidationUtils.ArgumentNotNull(type, "type");
            //ValidationUtils.ArgumentNotNull(genericInterfaceDefinition, "genericInterfaceDefinition");

            if (!genericInterfaceDefinition.IsInterface() || !genericInterfaceDefinition.IsGenericTypeDefinition())
                throw new ArgumentNullException(string.Format(CultureInfo.InvariantCulture,"'{0}' is not a generic interface definition.", genericInterfaceDefinition));

            if (type.IsInterface())
            {
                if (type.IsGenericType())
                {
                    Type interfaceDefinition = type.GetGenericTypeDefinition();

                    if (genericInterfaceDefinition == interfaceDefinition)
                    {
                        implementingType = type;
                        return true;
                    }
                }
            }

            foreach (Type i in type.GetInterfaces())
            {
                if (i.IsGenericType())
                {
                    Type interfaceDefinition = i.GetGenericTypeDefinition();

                    if (genericInterfaceDefinition == interfaceDefinition)
                    {
                        implementingType = i;
                        return true;
                    }
                }
            }

            implementingType = null;
            return false;
        }

        static readonly bool Is64BitProcess = (IntPtr.Size == 8);


#if !PORTABLE
        //private static int SizeOf(Type t)
        //{
        //    if (t == null)
        //    {
        //        throw new ArgumentNullException("t");
        //    }

        //    return Cache.GetOrAdd(t, t2 =>
        //    {
        //        var dm = new DynamicMethod("$", typeof(int), Type.EmptyTypes);
        //        ILGenerator il = dm.GetILGenerator();
        //        il.Emit(OpCodes.Sizeof, t2);
        //        il.Emit(OpCodes.Ret);

        //        var func = (Func<int>)dm.CreateDelegate(typeof(Func<int>));
        //        return func();
        //    });
        //}

        //private static readonly ConcurrentDictionary<Type, int>
        //    Cache = new ConcurrentDictionary<Type, int>();       
#endif
    }
   
}