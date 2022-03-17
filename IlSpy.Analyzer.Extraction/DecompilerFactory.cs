using System.Collections.Concurrent;

namespace IlSpy.Analyzer.Extraction;

public static class DecompilerFactory
{
    private static readonly ConcurrentDictionary<string, CachingDecompiler> _decompilers;

    static DecompilerFactory()
    {
        _decompilers = new ConcurrentDictionary<string, CachingDecompiler>();
    }

    public static CachingDecompiler Get(string fileName)
    {
        if (_decompilers.TryGetValue(fileName, out var result))
        {
            return result;
        }

        result = new CachingDecompiler(fileName);
        _decompilers.TryAdd(fileName, result);
        return result;
    }
}
