namespace AethericForge.Runtime.Util;

public static class DictionaryExtensions
{
    public static TValue GetOrThrow<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
    {
        if (dict.TryGetValue(key, out var value))
            return value;
        
        throw new KeyNotFoundException($"No item with ID '{key}' exists.");
    }
}
