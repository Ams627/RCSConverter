using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RcsConverter
{
    static class LinqExtensions
    {
        public static SortedSet<T> ToSortedSet<T>(this IEnumerable<T> source)
        {
            return new SortedSet<T>(source);
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
