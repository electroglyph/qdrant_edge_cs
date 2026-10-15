using System.Reflection;
using System.Runtime.InteropServices;

namespace Qdrant.Edge.Tests;

public sealed class InteropGapsTests
{
    private sealed class TestEdgeException : EdgeException
    {
        public TestEdgeException(string reason)
            : base(reason)
        {
        }

        public TestEdgeException(string reason, Exception? inner)
            : base(reason, inner)
        {
        }
    }

    [Fact]
    public void EdgeShardHandle_Default_IsInvalid()
    {
        EdgeShardHandle handle = new();
        try
        {
            Assert.True(handle.IsInvalid);
            Assert.Equal(nint.Zero, handle.DangerousGetHandle());
        }
        finally
        {
            GC.SuppressFinalize(handle);
        }
    }

    [Fact]
    public void EdgeShardHandle_NonZero_IsValid()
    {
        // Never dispose: release would call shard_destroy on a bogus pointer.
        // SuppressFinalize keeps the finalizer from doing the same.
        EdgeShardHandle handle = new((nint)1);
        try
        {
            Assert.False(handle.IsInvalid);
            Assert.Equal((nint)1, handle.DangerousGetHandle());
        }
        finally
        {
            GC.SuppressFinalize(handle);
        }
    }

    [Fact]
    public void NativeLoader_ExpectedVersion_MatchesCabiCargoToml()
    {
        Assert.Equal("0.10.0", NativeLoader.ExpectedVersion);
        Assert.Equal(NativeLoader.ExpectedVersion, ReadCabiCargoVersion());
    }

    [Fact]
    public void NativeLoader_IsAvailable_DoesNotThrow()
    {
        bool available = NativeLoader.IsAvailable;
        Assert.Equal(available, NativeLoader.IsAvailable);
    }

    [Fact]
    public void NativeLoader_EnsureLoaded_Idempotent()
    {
        bool first = NativeLoader.IsAvailable;
        bool second = NativeLoader.IsAvailable;
        Assert.Equal(first, second);
    }

    [Fact]
    public void NativeLoader_ConcurrentEnsureLoaded_Safe()
    {
        bool[] results = new bool[8];
        Parallel.For(0, results.Length, i => results[i] = NativeLoader.IsAvailable);
        Assert.All(results, r => Assert.Equal(results[0], r));
    }

    [Fact]
    public void NativeLoader_ResolverIgnoresOtherLibraries()
    {
        // Ensure the resolver registration (and its cached delegate) has run.
        _ = NativeLoader.IsAvailable;
        object? outcome = InvokeResolver("not-qdrant-edge");
        Assert.Equal(nint.Zero, outcome);
    }

    [Fact]
    public void GetRid_ReturnsKnownRid()
    {
        string rid = InvokeGetRid();
        Assert.Contains(rid, new[] { "win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64" });
    }

    [Fact]
    public void QdrantEdgeStatus_Values_AreZeroToThree()
    {
        Assert.Equal(0, (int)QdrantEdgeStatus.Ok);
        Assert.Equal(1, (int)QdrantEdgeStatus.InvalidArgument);
        Assert.Equal(2, (int)QdrantEdgeStatus.OperationError);
        Assert.Equal(3, (int)QdrantEdgeStatus.ShardClosed);
    }

    [Fact]
    public void EdgeException_ReasonEqualsMessage()
    {
        TestEdgeException ex = new("boom");
        Assert.Equal("boom", ex.Reason);
        Assert.Equal(ex.Reason, ex.Message);
    }

    [Fact]
    public void EdgeException_ReasonWithInner_EqualsMessage()
    {
        InvalidOperationException inner = new("cause");
        TestEdgeException ex = new("boom", inner);
        Assert.Equal("boom", ex.Reason);
        Assert.Equal(ex.Reason, ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void EdgeException_NullReason_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new TestEdgeException(null!));
        Assert.Throws<ArgumentNullException>(() => new TestEdgeException(null!, new InvalidOperationException()));
    }

    [Fact]
    public void EdgeException_EmptyReason_ThrowsArgument()
    {
        ArgumentException first = Assert.Throws<ArgumentException>(() => new TestEdgeException(string.Empty));
        Assert.Equal(typeof(ArgumentException), first.GetType());
        ArgumentException second = Assert.Throws<ArgumentException>(
            () => new TestEdgeException(string.Empty, new InvalidOperationException()));
        Assert.Equal(typeof(ArgumentException), second.GetType());
    }

    [Fact]
    public void ShardClosedException_Message_IsShardIsClosed()
    {
        ShardClosedException ex = new();
        Assert.Equal("shard is closed", ex.Reason);
        Assert.Equal("shard is closed", ex.Message);
        Assert.IsAssignableFrom<EdgeException>(ex);
    }

    [Fact]
    public void EdgeInvalidArgumentException_InnerPreserved()
    {
        InvalidOperationException inner = new("cause");
        EdgeInvalidArgumentException ex = new("bad argument", inner);
        Assert.Equal("bad argument", ex.Reason);
        Assert.Equal("bad argument", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void EdgeOperationException_InnerPreserved()
    {
        InvalidOperationException inner = new("cause");
        EdgeOperationException ex = new("engine failed", inner);
        Assert.Equal("engine failed", ex.Reason);
        Assert.Equal("engine failed", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    private static string InvokeGetRid()
    {
        MethodInfo? method = typeof(NativeLoader).GetMethod("GetRid", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, null));
    }

    private static object? InvokeResolver(string libraryName)
    {
        Type loaderType = typeof(NativeLoader);
        foreach (Type nested in loaderType.GetNestedTypes(BindingFlags.NonPublic))
        {
            foreach (FieldInfo field in nested.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.FieldType == typeof(DllImportResolver) && field.GetValue(null) is Delegate cached)
                {
                    return cached.DynamicInvoke(libraryName, loaderType.Assembly, null);
                }
            }
        }

        IEnumerable<MethodInfo> candidates = loaderType
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Concat(loaderType.GetNestedTypes(BindingFlags.NonPublic)
                .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)));
        foreach (MethodInfo candidate in candidates)
        {
            ParameterInfo[] parameters = candidate.GetParameters();
            if (candidate.ReturnType == typeof(nint)
                && candidate.Name.Contains("EnsureLoaded", StringComparison.Ordinal)
                && parameters.Length == 3
                && parameters[0].ParameterType == typeof(string)
                && parameters[1].ParameterType == typeof(Assembly))
            {
                return candidate.Invoke(null, [libraryName, loaderType.Assembly, null]);
            }
        }

        Assert.Fail("Resolver lambda not found via reflection; pin NativeLoader.cs:38-40 by code review.");
        return null;
    }

    private static string ReadCabiCargoVersion()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "native", "cabi", "Cargo.toml");
            if (File.Exists(candidate))
            {
                foreach (string line in File.ReadLines(candidate))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("version", StringComparison.Ordinal))
                    {
                        int first = trimmed.IndexOf('"');
                        int last = trimmed.LastIndexOf('"');
                        if (first >= 0 && last > first)
                        {
                            return trimmed.Substring(first + 1, last - first - 1);
                        }
                    }
                }

                Assert.Fail($"No version entry found in {candidate}.");
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.Fail($"native/cabi/Cargo.toml not found above {AppContext.BaseDirectory}.");
        return string.Empty;
    }
}
