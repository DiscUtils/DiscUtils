using System.Collections.Generic;

namespace LibraryTests.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> AsSingle<T>(this T item)
        {
            yield return item;
        }

        public static IEnumerable<T> Concat<T>(this IEnumerable<T> items, params T[] additionalItems)
        {
            foreach (T item in items)
            {
                yield return item;
            }

            foreach (T item in additionalItems)
            {
                yield return item;
            }
        }
    }
}
