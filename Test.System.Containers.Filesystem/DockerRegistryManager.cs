using System.Diagnostics;

namespace Test.System.Containers.Filesystem;

[TestClass]
public class DockerRegistryManager
{
    public const string BaseImage = "dotnet/runtime";
    public const string BaseImageSource = "mcr.microsoft.com/";
    public const string BaseImageTag = "6.0";
    public const string LocalRegistry = "localhost:5010";

    private static string s_registryContainerId;

    [AssemblyInitialize]
    public static void StartAndPopulateDockerRegistry(TestContext context)
    {
        Console.WriteLine(nameof(StartAndPopulateDockerRegistry));

        ProcessStartInfo startRegistry = new("docker", "run --rm --publish 5010:5000 --detach registry:2")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using Process registryProcess = Process.Start(startRegistry);
        Assert.IsNotNull(registryProcess);
        string registryContainerId = registryProcess.StandardOutput.ReadLine();
        // debugging purposes
        string everythingElse = registryProcess.StandardOutput.ReadToEnd();
        string errStream = registryProcess.StandardError.ReadToEnd();
        Assert.IsNotNull(registryContainerId);
        registryProcess.WaitForExit();
        Assert.AreEqual(0, registryProcess.ExitCode);

        s_registryContainerId = registryContainerId;

        Process pullBase = Process.Start("docker", $"pull {BaseImageSource}{BaseImage}:{BaseImageTag}");
        Assert.IsNotNull(pullBase);
        pullBase.WaitForExit();
        Assert.AreEqual(0, pullBase.ExitCode);

        Process tag = Process.Start("docker", $"tag {BaseImageSource}{BaseImage}:{BaseImageTag} {LocalRegistry}/{BaseImage}:{BaseImageTag}");
        Assert.IsNotNull(tag);
        tag.WaitForExit();
        Assert.AreEqual(0, tag.ExitCode);

        Process pushBase = Process.Start("docker", $"push {LocalRegistry}/{BaseImage}:{BaseImageTag}");
        Assert.IsNotNull(pushBase);
        pushBase.WaitForExit();
        Assert.AreEqual(0, pushBase.ExitCode);
    }

    [AssemblyCleanup]
    public static void ShutdownDockerRegistry()
    {
        Assert.IsNotNull(s_registryContainerId);

        Process shutdownRegistry = Process.Start("docker", $"stop {s_registryContainerId}");
        Assert.IsNotNull(shutdownRegistry);
        shutdownRegistry.WaitForExit();
        Assert.AreEqual(0, shutdownRegistry.ExitCode);
    }
}