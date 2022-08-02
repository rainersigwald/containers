using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Containers;

#nullable disable

namespace System.Containers.Tasks
{
    public class ParseContainerProperties : Microsoft.Build.Utilities.Task
    {


        /// <summary>
        /// The full base image name. mcr.microsoft.com/dotnet/runtime:6.0, for example.
        /// </summary>
        [Required]
        public string FullyQualifiedBaseImageName { get; set; }

        /// <summary>
        /// This is ContainerImage
        /// </summary>
        [Required]
        public string ContainerImageName { get; set; }

        [Required]
        public string ContainerImageTag { get; set; }

        [Output]
        public string ParsedContainerRegistry { get; private set; }

        [Output]
        public string ParsedContainerImage { get; private set; }

        [Output]
        public string ParsedContainerTag { get; private set; }

        [Output]
        public string NewContainerImageName { get; private set; }

        [Output]
        public string NewContainerTag { get; private set; }

        public override bool Execute()
        {
            if (ContainerHelpers.IsValidImageName(ContainerImageName))
            {
                Log.LogError("Invalid image name: {0}", ContainerImageName);
                return !Log.HasLoggedErrors;
            }
            
            if (ContainerHelpers.IsValidImageTag(ContainerImageTag))
            {
                Log.LogError("Invalid image tag: {0}", ContainerImageTag);
                return !Log.HasLoggedErrors;
            }

            if (!ContainerHelpers.TryParseFullyQualifiedContainerName(FullyQualifiedBaseImageName, 
                                                                      out string outputReg,
                                                                      out string outputImage,
                                                                      out string outputTag))
            {
                Log.LogError("Could not parse the given ContainerBaseImage: {0}", FullyQualifiedBaseImageName);
                return !Log.HasLoggedErrors;
            }

            if (BuildEngine != null)
            {
                Log.LogMessage("Parsed the following properties. Note: Spaces are replaced with dashes.");
                Log.LogMessage("Host: {0}", ParsedContainerRegistry);
                Log.LogMessage("Image: {0}", ParsedContainerImage);
                Log.LogMessage("Tag: {0}", ParsedContainerTag);
                Log.LogMessage("Image Name: {0}", NewContainerImageName);
                Log.LogMessage("Image Tag: {0}", NewContainerTag);
            }

            return !Log.HasLoggedErrors;
        }
    }
}