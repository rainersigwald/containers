using System.Containers;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace Test.System.Containers.Filesystem;

[TestClass]
public class EndToEnd
{
    private const string NewImageName = "dotnetcontainers/testimage";

    [TestMethod]
    public async Task ManuallyPackDotnetApplication()
    {
        DirectoryInfo d = new DirectoryInfo("MinimalTestApp");
        if (d.Exists)
        {
            d.Delete(recursive: true);
        }

        Process dotnetNew = Process.Start("dotnet", "new console -f net6.0 -o MinimalTestApp");
        Assert.IsNotNull(dotnetNew);
        await dotnetNew.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNew.ExitCode);

        // Build project

        Process publish = Process.Start("dotnet", "publish -bl MinimalTestApp -r linux-x64");
        Assert.IsNotNull(publish);
        await publish.WaitForExitAsync();
        Assert.AreEqual(0, publish.ExitCode);

        // Build the image

        Registry registry = new Registry(new Uri($"http://{DockerRegistryManager.LocalRegistry}"));

        Image x = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.BaseImageTag);

        Layer l = Layer.FromDirectory(Path.Join("MinimalTestApp", "bin", "Debug", "net6.0", "linux-x64", "publish"), "/app");

        x.AddLayer(l);

        x.SetEntrypoint("/app/MinimalTestApp");

        // Push the image back to the local registry

        await registry.Push(x, NewImageName, DockerRegistryManager.BaseImage);

        // pull it back locally

        Process pull = Process.Start("docker", $"pull {DockerRegistryManager.LocalRegistry}/{NewImageName}:latest");
        Assert.IsNotNull(pull);
        await pull.WaitForExitAsync();
        Assert.AreEqual(0, pull.ExitCode);

        // Run the image

        ProcessStartInfo runInfo = new("docker", $"run --rm --tty {DockerRegistryManager.LocalRegistry}/{NewImageName}:latest");
        Process run = Process.Start(runInfo);
        Assert.IsNotNull(run);
        await run.WaitForExitAsync();

        Assert.AreEqual(0, run.ExitCode);
    }

    [TestMethod]
    public async Task EndToEnd_NoAPI()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), nameof(EndToEnd_NoAPI)));
        DirectoryInfo pathForLocalNugetSource = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "NuGetSource"));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        if (pathForLocalNugetSource.Exists)
        {
            pathForLocalNugetSource.Delete(recursive: true);
        }

        newProjectDir.Create();
        pathForLocalNugetSource.Create();

        // 🤢
        DirectoryInfo nupkgPath = new DirectoryInfo(Assembly.GetAssembly(this.GetType()).Location).Parent.Parent.Parent.Parent;
        nupkgPath = nupkgPath.GetDirectories("package")[0];
        FileInfo[] nupkgs = nupkgPath.GetFiles("*.nupkg");
        if (nupkgs == null || nupkgs.Length == 0)
        {
            // Build System.Containers.Tasks.csproj & wait.
            // for now, fail.
            Assert.Fail("No nupkg found in expected package folder. You may need to rerun the build");
        }

        ProcessStartInfo info = new ProcessStartInfo
        {
            WorkingDirectory = newProjectDir.FullName,
            FileName = "dotnet",
            Arguments = "new console -f net7.0",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // Create the project to pack
        Process dotnetNew = Process.Start(info);
        Assert.IsNotNull(dotnetNew);
        await dotnetNew.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNew.ExitCode);

        // Give it a unique nugetconfig
        info.Arguments = "new nugetconfig";
        Process dotnetNewNugetConfig = Process.Start(info);
        Assert.IsNotNull(dotnetNewNugetConfig);
        await dotnetNewNugetConfig.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNewNugetConfig.ExitCode);

        info.Arguments = $"nuget add source {pathForLocalNugetSource.FullName} --name local-temp";

        // Set up temp folder as "nuget feed"
        Process dotnetNugetAddSource = Process.Start(info);
        Assert.IsNotNull(dotnetNugetAddSource);
        await dotnetNugetAddSource.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNugetAddSource.ExitCode);

        for (int i = 0; i < nupkgs.Length; i++)
        {
            // Push local nupkg to "nuget feed"
            info.Arguments = $"nuget push {nupkgs[i].FullName} --source local-temp";
            Process dotnetNugetPush = Process.Start(info);
            Assert.IsNotNull(dotnetNugetPush);
            await dotnetNugetPush.WaitForExitAsync();
            Assert.AreEqual(0, dotnetNugetPush.ExitCode);
        }

        // Add package to the project
        info.Arguments = $"add package System.Containers.Tasks --prerelease";
        Process dotnetPackageAdd = Process.Start(info);
        Assert.IsNotNull(dotnetPackageAdd);
        await dotnetPackageAdd.WaitForExitAsync();
        Assert.AreEqual(0, dotnetPackageAdd.ExitCode);

        info.Arguments = $"publish /p:publishprofile=defaultcontainer /p:runtimeidentifier=linux-x64 /bl" +
                          $" /p:ContainerBaseImage=http://{DockerRegistryManager.BaseImageSource}{DockerRegistryManager.BaseImage}:{DockerRegistryManager.BaseImageTag}" +
                          $" /p:ContainerImageName={NewImageName}" +
                          $" /p:ContainerOutputRegistry=http://{DockerRegistryManager.LocalRegistry}" +
                          $" /p:Version=1.0"; // this populates into $(ContainerImageTag)

        // Build & publish the project
        Process publish = Process.Start(info);
        Assert.IsNotNull(publish);
        await publish.WaitForExitAsync();
        Assert.AreEqual(0, publish.ExitCode);

        Process pull = Process.Start("docker", $"pull {DockerRegistryManager.LocalRegistry}/{NewImageName}:latest");
        Assert.IsNotNull(pull);
        await pull.WaitForExitAsync();
        Assert.AreEqual(0, pull.ExitCode);

        ProcessStartInfo runInfo = new("docker", $"run --rm --tty {DockerRegistryManager.LocalRegistry}/{NewImageName}:latest")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        Process run = Process.Start(runInfo);
        Assert.IsNotNull(run);
        string stdout = await run.StandardOutput.ReadToEndAsync();
        await run.WaitForExitAsync();

        Console.WriteLine("stdout: " + stdout);
        Console.WriteLine("stderr: " + await run.StandardError.ReadToEndAsync());

        Assert.AreEqual(0, run.ExitCode);
        newProjectDir.Delete(true);
        pathForLocalNugetSource.Delete(true);
    }
}