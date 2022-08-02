using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace System.Containers;

public class Image
{
    internal JsonNode manifest;
    internal JsonNode config;

    internal List<Layer> newLayers = new();

    public Image(JsonNode manifest, JsonNode config)
    {
        this.manifest = manifest;
        this.config = config;
    }

    public void AddLayer(Layer l)
    {
        newLayers.Add(l);
        manifest["layers"]!.AsArray().Add(l.Descriptor);
        config["rootfs"]!["diff_ids"]!.AsArray().Add(l.Descriptor.Digest); // TODO: this should be the descriptor of the UNCOMPRESSED tarball (once we turn on compression)
        RecalculateDigest();
    }

    private void RecalculateDigest() {
        manifest["config"]!["digest"] = GetSha(config);
    }

    public void SetEntrypoint(string executable, string[]? args = null)
    {
        JsonObject? configObject = config["config"]!.AsObject();

        if (configObject is null)
        {
            throw new NotImplementedException("Expected base image to have a config node");
        }

        configObject["Entrypoint"] = executable;

        if (args is null)
        {
            configObject.Remove("Cmd");
        }
        else
        {
            configObject["Cmd"] = new JsonArray(args.Where(s => !string.IsNullOrEmpty(s)).Select(s =>(JsonObject)s).ToArray());
        }

        RecalculateDigest();
    }

    public string WorkingDirectory {
        get => (string?)manifest["config"]!["WorkingDir"] ?? "";
        set {
            config["config"]!["WorkingDir"] = value;
            RecalculateDigest();
        }
    }

    public string GetSha(JsonNode json)
    {
        using SHA256 mySHA256 = SHA256.Create();
        byte[] hash = mySHA256.ComputeHash(Encoding.UTF8.GetBytes(json.ToJsonString()));

        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
