﻿using System;
using System.IO;
using Dropcraft.Common.Configuration;
using Dropcraft.Common.Package;
using Newtonsoft.Json.Linq;

namespace Dropcraft.Runtime.Configuration
{
    public class PackageManifestParser : IRuntimePackageConfigParser
    {
        /// <summary>
        /// ManifestNameTemplate allows to change installablePackage manifest file name from the default 'dropcraft.json'
        /// to something like 'myapp-dropcraft.json' by a new providing template.
        /// Example template: '{0}-dropcraft.json', where {0} will be replaced with the package ID
        /// </summary>
        public string ManifestNameTemplate { get; set; }

        public PackageManifestParser()
        {
            ManifestNameTemplate = "dropcraft.json";
        }

        public IRuntimeParsedPackageConfig Parse(PackageInfo packageInfo)
        {
            var parsedManifestFile = ParseManifest(packageInfo);
            return parsedManifestFile == null ? null : new ParsedPackageManifest(packageInfo, parsedManifestFile);
        }

        protected virtual JObject ParseManifest(PackageInfo packageInfo)
        {
            var manifestFile = packageInfo.ManifestFile;

            if (!File.Exists(manifestFile))
            {
                manifestFile = Path.Combine(packageInfo.InstallPath, String.Format(ManifestNameTemplate, packageInfo.Id));

                if (!File.Exists(manifestFile))
                    return null;
            }

            var text = File.ReadAllText(manifestFile);
            return JObject.Parse(text);
        }
    }
}