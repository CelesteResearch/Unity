using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Celeste.Unity.Editor
{
    internal static class RuntimeDebugValues
    {
        internal const int CollectionLimit = 512;

        private static readonly Dictionary<Type, MemberInfo[]>
            PrivateMemberCache = new Dictionary<Type, MemberInfo[]>();

        private static readonly Dictionary<Type, MemberInfo[]> MemberCache = new Dictionary<Type, MemberInfo[]>();

        internal static MemberInfo[] Members(Type type, bool includePrivate = false)
        {
            var cache = includePrivate ? PrivateMemberCache : MemberCache;
            if (cache.TryGetValue(type, out var cached)) return cached;
            var members = new List<MemberInfo>();
            foreach (var member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public |
                                                   (includePrivate ? BindingFlags.NonPublic : 0)))
            {
                if (member is FieldInfo field && !field.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    members.Add(field);
                else if (member is PropertyInfo property && property.GetGetMethod(includePrivate) != null &&
                         property.GetIndexParameters().Length == 0)
                    members.Add(property);
            }

            members.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            cached = members.ToArray();
            cache.Add(type, cached);
            return cached;
        }

        internal static object Read(object owner, MemberInfo member)
            => member is FieldInfo field ? field.GetValue(owner) : ((PropertyInfo)member).GetValue(owner);

        internal static bool IsScalar(object value)
            => value == null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum ||
               value is decimal || value is System.Numerics.Vector2 || value is System.Numerics.Vector3 ||
               value is System.Numerics.Vector4 || value is TimeSpan;

        internal static string Label(object value)
            => value == null ? "null" :
                IsScalar(value) ? Convert.ToString(value,
                    System.Globalization.CultureInfo.InvariantCulture) : value.GetType().Name;

        internal static bool Snapshot(object value, List<object> destination)
        {
            destination.Clear();
            if (!(value is IEnumerable sequence) || value is string) return false;
            var enumerator = sequence.GetEnumerator();
            try
            {
                while (destination.Count < CollectionLimit && enumerator.MoveNext())
                    destination.Add(enumerator.Current);
                return destination.Count == CollectionLimit && enumerator.MoveNext();
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
    }
}
