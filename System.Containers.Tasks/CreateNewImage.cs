using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace System.Containers.Tasks
{
    public class CreateNewImage : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// Ex: mcr.microsoft.com
        /// </summary>
        [Required]
        public string BaseRegistry { get; set; }

        /// <summary>
        /// Ex: dotnet/runtime
        /// </summary>
        [Required]
        public string BaseImageName { get; set; }

        /// <summary>
        /// Ex: 6.0
        /// </summary>
        [Required]
        public string BaseImageTag { get; set; }

        [Required]
        public string OutputRegistryURL { get; set; }

        /// <summary>
        /// Constructed from "$(MSBuildProjectDirectory)\$(PublishDir)"
        /// </summary>
        [Required]
        public string PublishDirectory { get; set; }

        /// <summary>
        /// $(ContainerWorkingDirectory)
        /// </summary>
        [Required]
        public string WorkingDirectory { get; set; }

        [Required]
        public string NewImageName { get; set; }

        [Required]
        public string Entrypoint { get; set; }

        /// <summary>
        /// Arguments to pass alongside Entrypoint.
        /// </summary>
        public string EntrypointArgs { get; set; }



        /// <summary>
        /// CreateNewImage needs to:
        /// 1. Pull a base image (needs parameters: URL, BaseImage, BaseImageTag)
        /// 2. Add output of build as a new layer
        /// 3. Push image back to some registry (needs parameters: OutputURL, NewName, EntryPoint)
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            if (string.IsNullOrEmpty(PublishDirectory) || !Directory.Exists(PublishDirectory))
            {
                Log.LogError(string.Format("PublishDirectory is either null or doesn't exist invalid. IsNullOrEmpty: {0}, Exists: {1}", string.IsNullOrEmpty(PublishDirectory), Directory.Exists(PublishDirectory)));
                return !Log.HasLoggedErrors;
            }

            Registry reg;

            try
            {
                reg = new Registry(new Uri(BaseRegistry, UriKind.RelativeOrAbsolute));
            }
            catch (Exception e)
            {
                if (BuildEngine != null)
                {
                    Log.LogError("Failed initializing the registry: {0}", e.Message);
                }
                return !Log.HasLoggedErrors;
            }

            Image image;
            try
            {
                image = reg.GetImageManifest(BaseImageName, BaseImageTag).Result;
            }
            catch (Exception ex)
            {
                if (BuildEngine != null)
                {
                    Log.LogError("Failed getting image manifest: {0}.", ex.Message);
                }
                return !Log.HasLoggedErrors;
            }

            if (BuildEngine != null)
            {
                Log.LogMessage($"Loading from directory: {PublishDirectory}");
            }
            Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory);
            image.AddLayer(newLayer);

            image.SetEntrypoint(Entrypoint, EntrypointArgs?.Split(' ').ToArray());

            Registry outputReg = new Registry(new Uri(OutputRegistryURL));

            try
            {
                outputReg.Push(image, NewImageName, BaseImageName).Wait();
            }
            catch (Exception e)
            {
                if (BuildEngine != null)
                {
                    Log.LogError("Failed to push to the output registry: {0}", e.Message);
                }
                return !Log.HasLoggedErrors;
            }

            return !Log.HasLoggedErrors;
        }
    }
}