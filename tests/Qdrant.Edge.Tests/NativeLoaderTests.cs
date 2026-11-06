namespace Qdrant.Edge.Tests;

public sealed class NativeLoaderTests
{
    [Fact]
    public void NativeSearchDirectories_AssemblyDirectoryDiffersFromHost_ProbesAssemblyFirst()
    {
        string[] directories = NativeLoader.NativeSearchDirectories("/plugin", "/server");

        Assert.Equal(["/plugin", "/server"], directories);
    }

    [Fact]
    public void NativeSearchDirectories_AssemblyDirectoryMatchesHost_ProbesOnce()
    {
        string[] directories = NativeLoader.NativeSearchDirectories("/app", "/app");

        Assert.Equal(["/app"], directories);
    }

    [Fact]
    public void NativeSearchDirectories_AssemblyHasNoLocation_FallsBackToHost()
    {
        string[] directories = NativeLoader.NativeSearchDirectories("", "/server");

        Assert.Equal(["/server"], directories);
    }
}
