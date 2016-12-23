﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Dropcraft.Common;
using Dropcraft.Common.Deployment;
using Dropcraft.Common.Logging;
using Dropcraft.Common.Package;
using Dropcraft.Runtime.Core;

namespace Dropcraft.Deployment.Core
{
    public class DeploymentTransaction : IDeploymentTransaction
    {
        private readonly string _backupFolder;
        private readonly List<string> _installedFile = new List<string>();
        private readonly List<string> _createdFolder = new List<string>();
        private readonly List<FileRecord> _deletedFiles = new List<FileRecord>();
        private static readonly ILog Logger = LogProvider.For<DeploymentTransaction>();

        private readonly List<PackageId> _deletedPackages = new List<PackageId>();
        private readonly List<ProductPackageInfo> _installedPackages = new List<ProductPackageInfo>();

        public ReadOnlyCollection<PackageId> DeletedPackages => _deletedPackages.AsReadOnly();
        public ReadOnlyCollection<ProductPackageInfo> InstalledPackages => _installedPackages.AsReadOnly();

        public DeploymentTransaction()
        {
            _backupFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            if (!Directory.Exists(_backupFolder))
            {
                Directory.CreateDirectory(_backupFolder);
            }
        }

        public void CreateFolder(string folder)
        {
            if (!Directory.Exists(folder))
            {
                _createdFolder.Add(folder);
                Logger.Trace($"Creating folder {folder}");
                Directory.CreateDirectory(folder);
            }
        }

        public void DeleteFile(IPackageFile fileInfo)
        {
            var fileName = fileInfo.FileName;
            var targetPath = Path.Combine(_backupFolder, Path.GetFileName(fileName));
            File.Copy(fileName, targetPath);
            _deletedFiles.Add(new FileRecord {OriginalFile = fileName, BackupFile = targetPath});

            Logger.Trace($"Deleting {fileName}");
            File.Delete(fileName);

            var folder = Path.GetDirectoryName(fileName);
            if (!Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Logger.Trace($"Removing empty folder {folder}");
                Directory.Delete(folder);
            }
        }

        public void InstallFile(PackageFileDeploymentInfo fileInfo)
        {
            Logger.Trace($"Installing file {fileInfo.FileName} to {fileInfo.TargetFileName}");
            if (File.Exists(fileInfo.TargetFileName))
            {
                Logger.Trace($"Conflict between {fileInfo.FileName} and {fileInfo.TargetFileName}");
                switch (fileInfo.ConflictResolution)
                {
                    case FileConflictResolution.KeepExisting:
                        Logger.Trace("Conflict resolved: keep existing file");
                        return;
                    case FileConflictResolution.Fail:
                        Logger.Trace("Conflict resolved: fail");
                        throw new IOException($"Deployment stopped because of conflict between {fileInfo.FileName} and {fileInfo.TargetFileName}");
                }

                Logger.Trace("Conflict resolved: override file");
                DeleteFile(new PackageFileInfo(fileInfo.TargetFileName));
            }
            else
            {
                var folder = Path.GetDirectoryName(fileInfo.TargetFileName);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
            }

            File.Copy(fileInfo.FileName, fileInfo.TargetFileName);
            _installedFile.Add(fileInfo.TargetFileName);
            Logger.Trace($"File {fileInfo.TargetFileName} copied");
        }

        public void TrackDeletedPackage(PackageId packageId)
        {
            _deletedPackages.Add(packageId);
        }

        public void TrackInstalledPackage(IPackageConfiguration packageConfiguration, IEnumerable<PackageFileInfo> packageFiles)
        {
            var packageInfo = new ProductPackageInfo {Configuration = packageConfiguration};
            packageInfo.Files.AddRange(packageFiles);
            _installedPackages.Add(packageInfo);
        }

        public void Commit()
        {
            _installedFile.Clear();
            _deletedFiles.Clear();
            _createdFolder.Clear();

            _deletedPackages.Clear();
            _installedPackages.Clear();
        }

        public void Dispose()
        {
            Rollback();

            if (Directory.Exists(_backupFolder))
            {
                Directory.Delete(_backupFolder, true);
            }
        }

        private void Rollback()
        {
            if (_installedFile.Count == 0 && _deletedFiles.Count == 0 && _createdFolder.Count == 0)
            {
                return;
            }

            Logger.Warn("Rolling back changes");

            foreach (var file in _installedFile)
            {
                if (File.Exists(file))
                {
                    Logger.Trace($"Rolling back file copy for {file}");
                    File.Delete(file);
                }
            }

            foreach (var fileRecord in _deletedFiles)
            {
                Logger.Trace($"Rolling back file deletion for {fileRecord.OriginalFile}");

                if (!File.Exists(fileRecord.BackupFile))
                {
                    Logger.Trace($"Backup file not found {fileRecord.BackupFile}");
                    continue;
                }

                var folder = Path.GetDirectoryName(fileRecord.OriginalFile);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.Copy(fileRecord.BackupFile, fileRecord.OriginalFile, true);
            }

            foreach (var folder in _createdFolder)
            {
                if (!Directory.EnumerateFileSystemEntries(folder).Any())
                {
                    Logger.Trace($"Rolling back folder creation {folder}");
                    Directory.Delete(folder);
                }
            }

            
            Commit();
        }
    }
    class FileRecord
    {
        public string OriginalFile { get; set; }
        public string BackupFile { get; set; }
    }
}