
using System.Collections;
using System.Diagnostics.CodeAnalysis;

public static class Extensions
{
    public static void SetValues<TKey, TValue>(this Dictionary<TKey, TValue> target, Dictionary<TKey, TValue> other)
    {
        foreach (var key in other.Keys)
        {
            target[key] = other[key];
        }
    }

    public static IEnumerable<TSource> WhereNotNull<TSource>(this IEnumerable<TSource?> source) => source.Where(item => item is not null);
}   