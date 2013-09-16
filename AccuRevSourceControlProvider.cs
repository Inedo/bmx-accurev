using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;

namespace Inedo.BuildMasterExtensions.AccuRev
{
    /// <summary>
    /// Implements the AccuRev source control provider.
    /// </summary>
    [ProviderProperties(
        "AccuRev",
        "Provides functionality for getting files, browsing folders, and applying labels in AccuRev SCM.")]
    [CustomEditor(typeof(AccuRevSourceControlProviderEditor))]
    public sealed class AccuRevSourceControlProvider : SourceControlProviderBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccuRevSourceControlProvider"/> class.
        /// </summary>
        public AccuRevSourceControlProvider()
        {
        }

        /// <summary>
        /// Gets or sets the location of accurev.exe.
        /// </summary>
        [Persistent]
        public string ExePath { get; set; }
        /// <summary>
        /// Gets or sets the user name used to log in to AccuRev.
        /// </summary>
        [Persistent]
        public string UserName { get; set; }
        /// <summary>
        /// Gets or sets the password of the user name used to log in to AccuRev.
        /// </summary>
        [Persistent]
        public string Password { get; set; }

        public override char DirectorySeparator
        {
            get { return '\\'; }
        }

        /// <summary>
        /// Returns the string to prefix AccuRev SCM paths with.
        /// </summary>
        private string RootPathPrefix
        {
            get { return string.Format("{0}.{0}", this.DirectorySeparator); }
        }

        public override bool IsAvailable()
        {
            return File.Exists(this.ExePath);
        }
        public override void GetLatest(string sourcePath, string targetPath)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException("sourcePath");
            if (targetPath == null)
                throw new ArgumentNullException("targetPath");

            var path = sourcePath.Split('/', '\\');
            int streamIndex = -1;
            for (int i = path.Length - 1; i >= 0; i--)
            {
                if (path[i].StartsWith(":"))
                {
                    streamIndex = i;
                    break;
                }
            }

            if (streamIndex == -1)
                throw new ArgumentException("Invalid source path; stream not specified.");

            var filePath = string.Empty;
            if (streamIndex < path.Length - 1)
                filePath = string.Join(this.DirectorySeparator.ToString(), path, streamIndex + 1, path.Length - streamIndex - 1);

            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            try
            {
                AccuRev("pop", "-fx", "-O", "-R", "-v", path[streamIndex].TrimStart(':'), "-L", tempPath, this.RootPathPrefix + filePath);
                Util.Files.CopyFiles(Path.Combine(tempPath, filePath), targetPath);
            }
            finally
            {
                Directory.Delete(tempPath, true);
            }
        }
        public override DirectoryEntryInfo GetDirectoryEntryInfo(string sourcePath)
        {
            var streams = GetStreams();

            ReadFiles(streams, streams.Name.Substring(1), this.RootPathPrefix, true);

            if (string.IsNullOrEmpty(sourcePath))
            {
                return new DirectoryEntryInfo("", "", new[] { streams.ToDirectoryEntryInfo(this.DirectorySeparator.ToString()) }, new FileEntryInfo[0]);
            }

            var path = sourcePath.Split('/', '\\');
            int streamIndex = -1;
            for (int i = path.Length - 1; i >= 0; i--)
            {
                if (path[i].StartsWith(":"))
                {
                    streamIndex = i;
                    break;
                }
            }

            if (streamIndex == -1)
                throw new ArgumentException("Invalid source path; stream not specified.");

            var filePath = string.Empty;
            if (streamIndex < path.Length - 1)
                filePath = path[streamIndex + 1];

            var entry = SelectEntry(streams, path[streamIndex]);
            ReadFiles(entry, path[streamIndex].Substring(1), this.RootPathPrefix + filePath, true);

            var selectedEntry = entry.Select(string.Join(this.DirectorySeparator.ToString(), path, streamIndex + 1, path.Length - streamIndex - 1), this.DirectorySeparator.ToString());
            if (selectedEntry != null)
                return selectedEntry.ToDirectoryEntryInfo(this.DirectorySeparator.ToString());
            else
                return null;
        }
        public override bool AlwaysRecursesPath(string sourcePath)
        {
            return true;
        }
        public override byte[] GetFileContents(string filePath)
        {
            throw new NotImplementedException();
        }
        public override void ValidateConnection()
        {
            LogIn();
            GetStreams();
        }
        public override string ToString()
        {
            return "Provides functionality for getting files and browsing folders in AccuRev SCM.";
        }

        private void LogIn()
        {
            AccuRev("login", this.UserName, this.Password);
        }
        private void ReadFiles(DirectoryEntryBuilder root, string stream, string path, bool recurse)
        {
            LogIn();

            var filesDoc = AccuRev("files", "-fx", "-s", stream, path);
            var elements = filesDoc.SelectNodes("//element");

            foreach (XmlElement element in elements)
            {
                if (string.Equals(element.GetAttribute("dir"), "yes", StringComparison.OrdinalIgnoreCase))
                {
                    var subdirName = element.GetAttribute("location");
                    int substart = subdirName.LastIndexOfAny(new[] { '/', '\\' });
                    subdirName = subdirName.Substring(substart + 1);

                    var subdir = root.Directories.Add(subdirName);
                    if (recurse)
                        ReadFiles(subdir, stream, element.GetAttribute("location"), true);
                }
                else
                {
                    var fileName = element.GetAttribute("location");
                    int nameStart = fileName.LastIndexOfAny(new[] { '/', '\\' });
                    fileName = fileName.Substring(nameStart + 1);

                    DateTime lastModified = new DateTime();
                    var modTime = element.GetAttribute("modTime");
                    if (!string.IsNullOrEmpty(modTime))
                    {
                        long seconds;
                        if (long.TryParse(modTime, out seconds))
                        {
                            lastModified = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            lastModified.AddTicks(seconds * 10000000L);
                        }
                    }

                    long fileSize = 0;
                    var sizeString = element.GetAttribute("size");
                    if (!string.IsNullOrEmpty(sizeString))
                        long.TryParse(sizeString, out fileSize);

                    root.AddFile(fileName, fileSize, lastModified, FileAttributes.Normal);
                }
            }
        }

        /// <summary>
        /// Returns all of the streams defined on the AccuRev server.
        /// </summary>
        /// <returns>Streams in AccuRev.</returns>
        private DirectoryEntryBuilder GetStreams()
        {
            var streamsDoc = AccuRev("show", "-fx", "streams");
            var rootElement = streamsDoc.SelectSingleNode("//stream[not(@basis)]") as XmlElement;
            if (rootElement == null)
                throw new InvalidOperationException("Unable to get list of streams from AccuRev.");

            var rootEntry = new DirectoryEntryBuilder(":" + rootElement.GetAttribute("name"));

            Action<DirectoryEntryBuilder> read = null;
            read = basis =>
                {
                    var streamElements = streamsDoc.SelectNodes("//stream[@basis='" + basis.Name.Substring(1) + "']");
                    foreach (XmlElement stream in streamElements)
                    {
                        var name = ":" + stream.GetAttribute("name");
                        var newEntry = basis.Directories.Add(name);
                        read(newEntry);
                    }
                };

            read(rootEntry);

            return rootEntry;
        }

        private static DirectoryEntryBuilder[] Flatten(DirectoryEntryBuilder root)
        {
            var list = new List<DirectoryEntryBuilder>();
            Flatten(root, list);
            return list.ToArray();
        }
        private static void Flatten(DirectoryEntryBuilder root, List<DirectoryEntryBuilder> list)
        {
            list.Add(root);
            foreach (var entry in root.Directories)
                Flatten(entry, list);
        }
        private static DirectoryEntryBuilder SelectEntry(DirectoryEntryBuilder entry, string name)
        {
            if (entry.Name == name)
                return entry;

            foreach (var sub in entry.Directories)
            {
                var res = SelectEntry(sub, name);
                if (res != null)
                    return res;
            }

            return null;
        }

        private XmlDocument AccuRev(string command, params string[] args)
        {
            return AccuRevPath(null, command, args);
        }
        private XmlDocument AccuRevPath(string workingDirectory, string command, params string[] args)
        {
            var argBuffer = new StringBuilder(command + " ");

            foreach (var arg in args)
            {
                if (arg.EndsWith("\\"))
                    argBuffer.AppendFormat("\"{0}\\\" ", arg);
                else
                    argBuffer.AppendFormat("\"{0}\" ", arg);
            }

            var startInfo = new ProcessStartInfo(this.ExePath, argBuffer.ToString())
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (!string.IsNullOrEmpty(workingDirectory))
                startInfo.WorkingDirectory = workingDirectory;

            var process = new Process()
            {
                StartInfo = startInfo
            };

            this.LogProcessExecution(startInfo);
            process.Start();

            var memoryStream = new MemoryStream();
            var buffer = new byte[512];
            int bytesRead;

            while (!process.HasExited)
            {
                while ((bytesRead = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    memoryStream.Write(buffer, 0, bytesRead);
                }

                Thread.Sleep(5);
            }

            while ((bytesRead = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                memoryStream.Write(buffer, 0, bytesRead);
            }

            memoryStream.Position = 0;

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    Path.GetFileName(this.ExePath) + " exited with " + process.ExitCode.ToString() +
                    " (expected 0): " + 
                    Encoding.UTF8.GetString(memoryStream.ToArray())
                    + "."
                    );

            var xmlReader = XmlReader.Create(memoryStream, new XmlReaderSettings() { ConformanceLevel = System.Xml.ConformanceLevel.Fragment });
            var doc = new XmlDocument();
            doc.Load(xmlReader);
            return doc;
        }
    }
}
