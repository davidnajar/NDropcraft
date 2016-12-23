﻿using System.Collections.Generic;
using System.Linq;
using Dropcraft.Common;
using Dropcraft.Common.Deployment;
using Dropcraft.Common.Logging;
using Dropcraft.Common.Package;
using Dropcraft.Deployment.NuGet;

namespace Dropcraft.Deployment.Core
{
    public abstract class DeploymentAction
    {
        private static readonly ILog Logger = LogProvider.For<DeploymentAction>();

        public bool IsUpdate { get; }

        public DeploymentContext DeploymentContext { get; }

        protected DeploymentAction(DeploymentContext deploymentContext, bool isUpdate)
        {
            IsUpdate = isUpdate;
            DeploymentContext = deploymentContext;
        }

        public void RaiseDeploymentEvent(PackageDeploymentEvent e, PackageId id)
        {
            e.Id = id;
            e.IsUpdateInProgress = IsUpdate;
            DeploymentContext.RaiseDeploymentEvent(e);
        }

        protected void Info(string message)
        {
            Logger.Info(message);
        }

        public abstract void Execute(IDeploymentTransaction transaction);
    }

    public class DownloadPackageAction : DeploymentAction
    {
        private readonly INuGetEngine _nuGetEngine;
        private readonly string _path;
        private readonly DeploymentPackageInfo _deploymentPackage;

        public DownloadPackageAction(DeploymentContext deploymentContext, bool isUpdate, DeploymentPackageInfo deploymentPackage,
            INuGetEngine nuGetEngine, string path) : base(deploymentContext, isUpdate)
        {
            _nuGetEngine = nuGetEngine;
            _path = path;
            _deploymentPackage = deploymentPackage;
        }

        public override void Execute(IDeploymentTransaction transaction)
        {
            _nuGetEngine.InstallPackage(_deploymentPackage.Match, _path).GetAwaiter().GetResult();
            _deploymentPackage.PackagePath = _nuGetEngine.GetPackageTargetPath(_deploymentPackage.Match.Library.Name,
                _deploymentPackage.Match.Library.Version, _path);

            RaiseDeploymentEvent(new AfterPackageDownloadedEvent { PackagePath = _deploymentPackage.PackagePath }, _deploymentPackage.Id);
            Info($"{_deploymentPackage.Id} downloaded");
        }
    }

    public class DeletePackageAction : DeploymentAction
    {
        private readonly PackageId _packageId;

        public DeletePackageAction(DeploymentContext deploymentContext, bool isUpdate, PackageId packageId)
            : base(deploymentContext, isUpdate)
        {
            _packageId = packageId;
        }

        public override void Execute(IDeploymentTransaction transaction)
        {
            var files = DeploymentContext.ProductConfigurationProvider.GetInstalledFiles(_packageId, true);

            var deleteEvent = new BeforePackageUninstalledEvent();
            deleteEvent.FilesToDelete.AddRange(files);
            RaiseDeploymentEvent(deleteEvent, _packageId);

            foreach (var file in deleteEvent.FilesToDelete)
            {
                transaction.DeleteFile(file);
            }

            DeploymentContext.ProductConfigurationProvider.RemovePackage(_packageId);
            transaction.TrackDeletedPackage(_packageId);

            RaiseDeploymentEvent(new AfterPackageUninstalledEvent(), _packageId);
            Info($"{_packageId} uninstalled");
        }
    }

    public class InstallPackageAction : DeploymentAction
    {
        private readonly DeploymentPackageInfo _deploymentPackage;
        private readonly IPackageConfigurationProvider _packageConfigProvider;
        private readonly IDeploymentStartegyProvider _deploymentStartegy;

        public InstallPackageAction(DeploymentContext deploymentContext, bool isUpdate, DeploymentPackageInfo deploymentPackage,
            IDeploymentStartegyProvider deploymentStartegy) : base(deploymentContext, isUpdate)
        {
            _deploymentPackage = deploymentPackage;
            _packageConfigProvider = DeploymentContext.PackageConfigurationProvider;
            _deploymentStartegy = deploymentStartegy;
        }

        public override void Execute(IDeploymentTransaction transaction)
        {
            var files = _deploymentStartegy.GetPackageFiles(_deploymentPackage.Id, _deploymentPackage.PackagePath).ToList();
            var installedFiles = new List<PackageFileInfo>();

            var e = new BeforePackageInstalledEvent();
            e.FilesToInstall.AddRange(files);
            RaiseDeploymentEvent(e, _deploymentPackage.Id);

            var productPathLength = DeploymentContext.ProductPath.Length;

            foreach (var file in e.FilesToInstall)
            {
                if (file.Action == FileAction.Copy)
                {
                    transaction.InstallFile(file);

                    var filePath = file.TargetFileName;
                    if (filePath.StartsWith(DeploymentContext.ProductPath))
                    {
                        filePath = filePath.Remove(0, productPathLength).TrimStart('/', '\\');
                    }

                    installedFiles.Add(new PackageFileInfo(filePath));
                }
                else if (file.Action == FileAction.Delete)
                {
                    transaction.DeleteFile(new PackageFileInfo(file.TargetFileName));
                }
            }

            var cfg = _packageConfigProvider.GetPackageConfiguration(_deploymentPackage.Id, _deploymentPackage.PackagePath);
            transaction.TrackInstalledPackage(cfg, installedFiles);

            RaiseDeploymentEvent(new AfterPackageInstalledEvent(), _deploymentPackage.Id);
            Info($"{_deploymentPackage.Id} installed");
        }
    }
}