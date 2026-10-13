using System.Runtime.InteropServices;

namespace Qdrant.Edge;

/// <summary>
/// Resolves and loads the native <c>qdrant_edge</c> library per RID.
/// </summary>
public static class NativeLoader
{
    private static readonly Lock Sync = new();
    private static bool _resolverRegistered;
    private static bool _loadAttempted;
    private static bool _loadSucceeded;

    /// <summary>Whether the native library was loaded successfully.</summary>
    public static bool IsAvailable
    {
        get
        {
            EnsureLoaded();
            return _loadSucceeded;
        }
    }

    /// <summary>Library version synced with ffi/python/amalgamate.py.</summary>
    public const string ExpectedVersion = "0.9.0";

    internal static void EnsureLoaded()
    {
        lock (Sync)
        {
            if (!_resolverRegistered)
            {
                NativeLibrary.SetDllImportResolver(
                    typeof(NativeLoader).Assembly,
                    static (name, assembly, path) =>
                    {
                        if (name is not NativeMethods.LibraryName)
                        {
                            return nint.Zero;
                        }

                        return ResolveRidLibrary();
                    });
                _resolverRegistered = true;
            }

            if (_loadAttempted)
            {
                return;
            }

            _loadAttempted = true;
            // Probe with the same RID resolution the DllImport resolver uses.
            // NativeLibrary.Load does not consult DllImport resolvers, so
            // asking it to load by bare name would always miss the
            // runtimes/<rid>/native asset and report a false negative.
            _loadSucceeded = ResolveRidLibrary() != nint.Zero;
        }
    }

    private static nint ResolveRidLibrary()
    {
        string rid = GetRid();
        string fileName = OperatingSystem.IsWindows() ? "qdrant_edge.dll"
            : OperatingSystem.IsMacOS() ? "libqdrant_edge.dylib"
            : "libqdrant_edge.so";
        string candidate = Path.Combine(
            AppContext.BaseDirectory, "runtimes", rid, "native", fileName);
        if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out nint handle))
        {
            return handle;
        }

        return nint.Zero;
    }

    private static string GetRid() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 when OperatingSystem.IsWindows() => "win-x64",
        Architecture.Arm64 when OperatingSystem.IsWindows() => "win-arm64",
        Architecture.X64 when OperatingSystem.IsMacOS() => "osx-x64",
        Architecture.Arm64 when OperatingSystem.IsMacOS() => "osx-arm64",
        Architecture.X64 => "linux-x64",
        Architecture.Arm64 => "linux-arm64",
        _ => "linux-x64",
    };
}
