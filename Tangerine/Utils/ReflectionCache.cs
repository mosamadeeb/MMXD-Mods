using Fasterflect;
using System;
using System.Collections.Generic;

namespace Tangerine.Utils
{
    internal static class ReflectionCache
    {
        private static readonly Dictionary<Type, Dictionary<string, Accessor>> _propertyCache = new();

        public struct Accessor
        {
            public string Name;
            public MemberGetter Getter;
            public MemberSetter Setter;

            public Accessor(Type type, string propertyName)
            {
                Name = propertyName;
                Getter = type.DelegateForGetPropertyValue(propertyName);
                Setter = type.DelegateForSetPropertyValue(propertyName);
            }
        }

        public static Accessor GetPropertyAccessor(Type type, string propertyName)
        {
            if (!_propertyCache.TryGetValue(type, out Dictionary<string, Accessor> delegateCache))
            {
                delegateCache = new Dictionary<string, Accessor>();
                _propertyCache.Add(type, delegateCache);
            }

            if (!delegateCache.TryGetValue(propertyName, out Accessor accessor))
            {
                accessor = new Accessor(type, propertyName);
                delegateCache.Add(propertyName, accessor);
            }

            return accessor;
        }

        public static MemberGetter GetPropertyGetter(Type type, string propertyName)
        {
            return GetPropertyAccessor(type, propertyName).Getter;
        }

        public static MemberSetter GetPropertySetter(Type type, string propertyName)
        {
            return GetPropertyAccessor(type, propertyName).Setter;
        }

        public static object GetPropertyCached(object obj, string propertyName)
        {
            return GetPropertyGetter(obj.GetType(), propertyName)(obj);
        }

        public static void SetPropertyCached(object obj, string propertyName, object value)
        {
            GetPropertySetter(obj.GetType(), propertyName)(obj, value);
        }
    }
}
