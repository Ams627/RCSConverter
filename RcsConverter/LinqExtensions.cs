using System.Collections.Generic;

namespace RcsConverter
{
    static class LinqExtensions
    {
        public static SortedSet<T> ToSortedSet<T>(this IEnumerable<T> source)
        {
            return new SortedSet<T>(source);
        }

        public static SortedSet<T> ToSortedSet<T>(this IEnumerable<T> source, IComparer<T> comparer)
        {
            return new SortedSet<T>(source, comparer);
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            return new HashSet<T>(source);
        }
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer)
        {
            return new HashSet<T>(source, comparer);
        }


    }
}
