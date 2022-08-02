using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace System.Containers;

public class Image
{
    public JsonNode manifest;
    public JsonNode config;

    internal readonly Registry? originatingRegistry;

    internal List<Layer> newLayers = new();

    public Image(JsonNode manifest, JsonNode config, Registry? registry)
    {
        this.manifest = manifest;
        this.config = config;
        this.originatingRegistry = registry;
    }

    public void AddLayer(Layer l)
    {
        newLayers.Add(l);
        manifest["layers"]!.AsArray().Add(l.Descriptor);
        config["rootfs"]!["diff_ids"]!.AsArray().Add(l.Descriptor.Digest); // TODO: this should be the descriptor of the UNCOMPRESSED tarball (once we turn on compression)
        RecalculateDigest();
    }

    private void RecalculateDigest() {
        manifest["config"]!["digest"] = GetDigest(config);
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
            configObject["Cmd"] = new JsonArray(args.Select(s =>(JsonObject)s).ToArray());
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

    public string GetDigest(JsonNode json)
    {
        string hashString;

        hashString = GetSha(json);

        return $"sha256:{hashString}";
    }

    public static string GetSha(JsonNode json)
    {
        using SHA256 mySHA256 = SHA256.Create();
        byte[] hash = mySHA256.ComputeHash(Encoding.UTF8.GetBytes(json.ToJsonString()));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
