namespace Concertable.Payment.Infrastructure;

internal static class DictionaryExtensions
{
    internal static Dictionary<string, string> Merge(
        this Dictionary<string, string> seed,
        IDictionary<string, string>? extra)
    {
        if (extra is not null)
            foreach (var kvp in extra)
                seed.TryAdd(kvp.Key, kvp.Value);
        return seed;
    }

    internal static Dictionary<string, string> With(
        this IDictionary<string, string> source,
        string key,
        string value)
    {
        var result = source.ToDictionary(kv => kv.Key, kv => kv.Value);
        result[key] = value;
        return result;
    }
}
