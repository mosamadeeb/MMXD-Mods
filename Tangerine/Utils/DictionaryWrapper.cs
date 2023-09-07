using Fasterflect;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Tangerine.Utils
{
    internal class DictionaryWrapper<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly object _obj;

        private readonly MemberGetter _keysGetter, _valuesGetter, _countGetter;
        private readonly MethodInvoker _getIndexer, _setIndexer, _containsKey, _remove;

        public readonly Type KeyType;
        public readonly Type ValueType;

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => (ICollection <TKey>)_keysGetter(_obj);
        ICollection<TValue> IDictionary<TKey, TValue>.Values => (ICollection <TValue>)_valuesGetter(_obj);
        int ICollection<KeyValuePair<TKey, TValue>>.Count => (int)_countGetter(_obj);
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        public TValue this[TKey key]
        {
            get { return (TValue)_getIndexer(_obj, key); }
            set { _setIndexer(_obj, key, value); }
        }

        public DictionaryWrapper(object obj, Type keyType = null, Type valueType = null)
        {
            _obj = obj;
            var objType = obj.GetType();

            KeyType = keyType ?? typeof(TKey);
            ValueType = valueType ?? typeof(TValue);

            _keysGetter = objType.DelegateForGetPropertyValue("Keys");
            _valuesGetter = objType.DelegateForGetPropertyValue("Values");
            _countGetter = objType.DelegateForGetPropertyValue("Count");

            _getIndexer = objType.DelegateForGetIndexer(KeyType);
            _setIndexer = objType.DelegateForSetIndexer(KeyType, ValueType);

            _containsKey = objType.DelegateForCallMethod("ContainsKey", KeyType);
            _remove = objType.DelegateForCallMethod("Remove", KeyType);
        }

        public void Add(TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

             if (ContainsKey(key))
                throw new ArgumentException(nameof(key));

            this[key] = value;
        }

        public bool ContainsKey(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return (bool)_containsKey(_obj, key);
        }

        public bool Remove(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return (bool)_remove(key);
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (ContainsKey(key))
            {
                value = this[key];
                return true;
            }

            value = default(TValue);
            return false;
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
