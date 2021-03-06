using System.Formats.Tar;
using System.Security.Cryptography;

namespace System.Containers;

public record struct Layer
{
    public Descriptor Descriptor { get; private set; }

    public string BackingFile { get; private set; }

    public static Layer FromDirectory(string directory, string containerPath)
    {
        DirectoryInfo di = new(directory);

        IEnumerable<(string path, string containerPath)> fileList = 
            di.GetFileSystemInfos()
                .Where(fsi => fsi is FileInfo).Select(
                fsi =>
                {
                    string destinationPath =
                        Path.Join(containerPath,
                            Path.GetRelativePath(directory, fsi.FullName))
                        .Replace(Path.DirectorySeparatorChar, '/');
                    return (fsi.FullName, destinationPath);
                });

        return FromFiles(fileList);
    }

    public static Layer FromFiles(IEnumerable<(string path, string containerPath)> fileList)
    {
        string tempPath = Path.Join(Configuration.ArtifactRoot, "Temp");

        Directory.CreateDirectory(tempPath);

        long fileSize;
        byte[] hash;

        string tempTarballPath = Path.Join(tempPath, Path.GetRandomFileName());
        using (FileStream fs = File.Create(tempTarballPath))
        {
            // using (GZipStream gz = new(fs, CompressionMode.Compress)) // TODO: https://github.com/rainersigwald/containers/issues/29
            using (TarWriter writer = new(fs, TarEntryFormat.Gnu, leaveOpen: true))
            {
                foreach (var item in fileList)
                {
                    // Docker treats a COPY instruction that copies to a path like `/app` by
                    // including `app/` as a directory, with no leading slash. Emulate that here.
                    string containerPath = item.containerPath.TrimStart(PathSeparators);

                    writer.WriteEntry(item.path, containerPath);
                }
            }

            fileSize = fs.Length;

            fs.Position = 0;

            using SHA256 mySHA256 = SHA256.Create();
            hash = mySHA256.ComputeHash(fs);
        }

        string contentHash = Convert.ToHexString(hash).ToLowerInvariant();

        string storedContent = Path.Combine(Configuration.ContentRoot, contentHash);

        Directory.CreateDirectory(Configuration.ContentRoot);

        File.Move(tempTarballPath, storedContent, overwrite: true);

        Layer l = new()
        {
            Descriptor = new()
            {
                MediaType = "application/vnd.docker.image.rootfs.diff.tar", // TODO: configurable? gzip always?
                Size = fileSize,
                Digest = $"sha256:{contentHash}"
            },
            BackingFile = storedContent,
        };

        return l;
    }

    private readonly static char[] PathSeparators = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

}