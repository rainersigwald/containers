using Microsoft.VisualBasic;

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace System.Containers;

public record struct Registry(Uri BaseUri)
{
    private const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";
    private const string DockerContainerV1 = "application/vnd.docker.container.image.v1+json";

    public async Task<Image> GetImageManifest(string name, string reference)
    {
        using HttpClient client = GetClient();

        var response = await client.GetAsync(new Uri(BaseUri, $"/v2/{name}/manifests/{reference}"));

        response.EnsureSuccessStatusCode();

        var s = await response.Content.ReadAsStringAsync();

        var manifest = JsonNode.Parse(s);

        if (manifest is null) throw new NotImplementedException("Got a manifest but it was null");

        if ((string?)manifest["mediaType"] != DockerManifestV2)
        {
            throw new NotImplementedException($"Do not understand the mediaType {manifest["mediaType"]}");
        }

        JsonNode? config = manifest["config"];
        Debug.Assert(config is not null);
        Debug.Assert(((string?)config["mediaType"]) == DockerContainerV1);

        string? configSha = (string?)config["digest"];
        Debug.Assert(configSha is not null);

        response = await client.GetAsync(new Uri(BaseUri, $"/v2/{name}/blobs/{configSha}"));

        JsonNode? configDoc = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Debug.Assert(configDoc is not null);
        //Debug.Assert(((string?)configDoc["mediaType"]) == DockerContainerV1);

        return new Image(manifest, configDoc);
    }

    public async Task Push(Layer layer, string name)
    {
        string digest = layer.Descriptor.Digest;

        using (FileStream contents = File.OpenRead(layer.BackingFile))
        {
            await UploadBlob(name, digest, contents);
        }
    }

    private readonly async Task UploadBlob(string name, string digest, Stream contents)
    {
        using HttpClient client = GetClient();

        if (await BlobAlreadyUploaded(name, digest, client))
        {
            // Already there!
            return;
        }

        HttpResponseMessage pushResponse = await client.PostAsync(new Uri(BaseUri, $"/v2/{name}/blobs/uploads/"), content: null);

        Debug.Assert(pushResponse.StatusCode == Net.HttpStatusCode.Accepted);

        //Uri uploadUri = new(BaseUri, pushResponse.Headers.GetValues("location").Single() + $"?digest={layer.Descriptor.Digest}");
        Debug.Assert(pushResponse.Headers.Location is not null);

        var x = new UriBuilder(pushResponse.Headers.Location);

        x.Query += $"&digest={Uri.EscapeDataString(digest)}";

        // TODO: consider chunking
        StreamContent content = new StreamContent(contents);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = contents.Length;
        HttpResponseMessage putResponse = await client.PutAsync(x.Uri, content);

        string resp = await putResponse.Content.ReadAsStringAsync();

        putResponse.EnsureSuccessStatusCode();
    }

    private readonly async Task<bool> BlobAlreadyUploaded(string name, string digest, HttpClient client)
    {
        HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri(BaseUri, $"/v2/{name}/blobs/{digest}")));

        if (response.StatusCode == Net.HttpStatusCode.OK)
        {
            return true;
        }

        return false;
    }

    private static HttpClient GetClient()
    {
        HttpClient client = new(new HttpClientHandler() { UseDefaultCredentials = true });

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerManifestV2));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerContainerV1));

        client.DefaultRequestHeaders.Add("User-Agent", ".NET Container Library");

        return client;
    }

    public async Task Push(Image x, string name, string baseName)
    {
        using HttpClient client = GetClient();

        foreach (var layerJson in x.manifest["layers"].AsArray())
        {
            JsonNode? layerValue = JsonValue.Parse(layerJson.ToJsonString());

            JsonNode? digestNode = layerValue["digest"];
            string digest = digestNode.ToString();

            if (await BlobAlreadyUploaded(name, digest, client))
            {
                continue;
            }

            // Blob wasn't there; can we tell the server to get it from the base image?
            HttpResponseMessage pushResponse = await client.PostAsync(new Uri(BaseUri, $"/v2/{name}/blobs/uploads/?mount={digest}&from={baseName}"), content: null);

            if (pushResponse.StatusCode != HttpStatusCode.Created)
            {
                // The blob wasn't already available in another namespace, so fall back to explicitly uploading it

                // TODO: don't do this search, which is ridiculous
                foreach (Layer layer in x.newLayers)
                {
                    if (layer.Descriptor.Digest == digest)
                    {
                        await Push(layer, name);
                        break;
                    }

                    throw new NotImplementedException("Need to push a layer but it's not a new one--need to download it from the base registry and upload it");
                }
            }
        }

        using (MemoryStream stringStream = new MemoryStream(Encoding.UTF8.GetBytes(x.config.ToJsonString())))
        {
            await UploadBlob(name, x.GetSha(x.config), stringStream);
        }

        HttpContent manifestUploadContent = new StringContent(x.manifest.ToJsonString());
        manifestUploadContent.Headers.ContentType = new MediaTypeHeaderValue(DockerManifestV2);

        var putResponse = await client.PutAsync(new Uri(BaseUri, $"/v2/{name}/manifests/{x.GetSha(x.manifest)}"), manifestUploadContent);

        string putresponsestr = await putResponse.Content.ReadAsStringAsync();

        putResponse.EnsureSuccessStatusCode();

        var putResponse2 = await client.PutAsync(new Uri(BaseUri, $"/v2/{name}/manifests/latest"), manifestUploadContent);

        putResponse2.EnsureSuccessStatusCode();
    }
}