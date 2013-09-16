using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.IssueTracking;
using Inedo.BuildMaster.Web;
using Inedo.Proxies;

namespace Inedo.BuildMasterExtensions.AccuRev
{
    /// <summary>
    /// Implements an issue tracking provider for AccuWork.
    /// </summary>
    [ProviderProperties(
        "AccuWork",
        "Provides integration with the AccuWork issue tracking system.")]
    [CustomEditor(typeof(AccuWorkIssueTrackingProviderEditor))]
    public sealed class AccuWorkIssueTrackingProvider : IssueTrackingProviderBase, ICategoryFilterable
    {
        private SchemaInfo schema;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccuWorkIssueTrackingProvider"/> class.
        /// </summary>
        public AccuWorkIssueTrackingProvider()
        {
        }

        /// <summary>
        /// Gets or sets the category ID filter.
        /// </summary>
        public string[] CategoryIdFilter { get; set; }
        /// <summary>
        /// Gets an inheritor-defined array of category types.
        /// </summary>
        [ShouldProxyExecute]
        public string[] CategoryTypeNames
        {
            get
            {
                if (string.IsNullOrEmpty(this.FilterCategory))
                    return new string[0];

                EnsureSchema(false);

                return new[] { this.schema.CategoryName };
            }
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
        /// <summary>
        /// Gets or sets the name of the depot which contains the issue database.
        /// </summary>
        [Persistent]
        public string DepotName { get; set; }
        /// <summary>
        /// Gets or sets the field name which contains an issue's ID.
        /// </summary>
        [Persistent]
        public string IssueIdField { get; set; }
        /// <summary>
        /// Gets or sets the field name which contains an issue's associated
        /// target release.
        /// </summary>
        [Persistent]
        public string ReleaseField { get; set; }
        /// <summary>
        /// Gets or sets the field name which contains an issue's title or
        /// short description.
        /// </summary>
        [Persistent]
        public string TitleField { get; set; }
        /// <summary>
        /// Gets or sets the field name which contains an issue's description.
        /// </summary>
        [Persistent]
        public string DescriptionField { get; set; }
        /// <summary>
        /// Gets or sets the field name which contains an issue's status.
        /// </summary>
        [Persistent]
        public string StatusField { get; set; }
        /// <summary>
        /// Gets or sets the statuses which correspond to a "closed" issue state.
        /// </summary>
        [Persistent]
        public string[] ClosedStatuses { get; set; }
        /// <summary>
        /// Gets or sets the field name of category to filter by.
        /// </summary>
        [Persistent]
        public string FilterCategory { get; set; }

        /// <summary>
        /// Returns an array of <see cref="Issue"/> objects that are for the current
        /// release.
        /// </summary>
        /// <param name="releaseNumber">Release number of issues to return.</param>
        /// <returns>
        /// Array of issues for the specified release.
        /// </returns>
        public override Issue[] GetIssues(string releaseNumber)
        {
            EnsureSchema(false);

            var releaseCondition = string.IsNullOrEmpty(releaseNumber)
                ? null
                : string.Format("{0} == \"{1}\"", this.schema.ReleaseFieldId, releaseNumber);

            var categoryCondition = string.IsNullOrEmpty(this.FilterCategory)
                ? null
                : string.Format("{0} == \"{1}\"", this.schema.CategoryFieldId, this.CategoryIdFilter[0]);

            var queryFileName = Path.GetTempFileName();
            try
            {
                using (var xmlWriter = XmlWriter.Create(queryFileName, new XmlWriterSettings() { OmitXmlDeclaration = true }))
                {
                    xmlWriter.WriteStartElement("queryIssue");
                    xmlWriter.WriteAttributeString("issueDB", this.DepotName);

                    if (releaseCondition != null && categoryCondition != null)
                    {
                        xmlWriter.WriteAttributeString("useAltQuery", "false");

                        xmlWriter.WriteStartElement("AND");
                        xmlWriter.WriteElementString("condition", releaseCondition);
                        xmlWriter.WriteElementString("condition", categoryCondition);
                        xmlWriter.WriteEndElement();
                    }
                    else if (releaseCondition != null)
                        xmlWriter.WriteString(releaseCondition);
                    else if (categoryCondition != null)
                        xmlWriter.WriteString(categoryCondition);

                    xmlWriter.WriteEndElement();
                }

                var issues = new List<AccuWorkIssue>();
                var results = AccuRev("xml", "-l", queryFileName);
                var issueElements = results.SelectNodes("//issue");

                foreach (XmlElement issueElement in issueElements)
                {
                    var id = ReadFieldValue(issueElement, this.schema.IssueIdFieldId);
                    var title = ReadFieldValue(issueElement, this.schema.TitleFieldId);
                    var description = ReadFieldValue(issueElement, this.schema.DescriptionFieldId);
                    var status = ReadFieldValue(issueElement, this.schema.StatusFieldId);

                    issues.Add(new AccuWorkIssue(id, status, title, description, releaseNumber));
                }

                return issues.ToArray();
            }
            finally
            {
                File.Delete(queryFileName);
            }
        }
        /// <summary>
        /// Returns a value indicating if the specified issue is closed.
        /// </summary>
        /// <param name="issue">Issue to check for a closed state.</param>
        /// <returns>
        /// True if issue is closed; otherwise false.
        /// </returns>
        public override bool IsIssueClosed(Issue issue)
        {
            if (issue == null)
                throw new ArgumentNullException("issue");

            if (this.ClosedStatuses == null || this.ClosedStatuses.Length == 0)
                return false;

            EnsureSchema(false);

            return Array.IndexOf<string>(this.ClosedStatuses, issue.IssueStatus) >= 0;
        }
        /// <summary>
        /// When implemented in a derived class, indicates whether the provider
        /// is installed and available for use in the current execution context.
        /// </summary>
        /// <returns></returns>
        public override bool IsAvailable()
        {
            return File.Exists(this.ExePath);
        }
        /// <summary>
        /// When implemented in a derived class, attempts to connect with the
        /// current configuration and, if not successful, throws a
        /// <see cref="ConnectionException"/>.
        /// </summary>
        public override void ValidateConnection()
        {
            EnsureSchema(true);
        }
        /// <summary>
        /// Returns an array of all appropriate categories defined within the provider.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// The nesting level (i.e. <see cref="CategoryBase.SubCategories"/>) can never be less than
        /// the length of <see cref="CategoryTypeNames"/>.
        /// </remarks>
        public CategoryBase[] GetCategories()
        {
            if (string.IsNullOrEmpty(this.FilterCategory))
                return new AccuWorkCategory[0];

            EnsureSchema(false);

            var categories = new List<AccuWorkCategory>();
            foreach (var name in this.schema.ValidCategoryValues)
                categories.Add(new AccuWorkCategory(name));

            return categories.ToArray();
        }
        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return "Provides integration with the AccuWork issue tracking system.";
        }

        /// <summary>
        /// Reads a field value.
        /// </summary>
        /// <param name="issue">The issue element.</param>
        /// <param name="fieldId">The field ID.</param>
        /// <returns>The value of the specified field.</returns>
        private static string ReadFieldValue(XmlElement issue, int fieldId)
        {
            var fieldElement = issue.SelectSingleNode(string.Format("./*[@fid='{0}']", fieldId)) as XmlElement;
            if (fieldElement == null)
                return null;

            return fieldElement.InnerText;
        }

        /// <summary>
        /// Verifies that the schema field has been populated.
        /// </summary>
        /// <param name="force">Value indicating whether the schema update should be forced.</param>
        private void EnsureSchema(bool force)
        {
            AccuRev("login", this.UserName ?? string.Empty, this.Password ?? string.Empty);

            if (this.schema == null)
            {
                var schemaDoc = AccuRev("getconfig", "-p", this.DepotName, "-r", "schema.xml");
                this.schema = SchemaInfo.Parse(
                    schemaDoc,
                    this.IssueIdField,
                    this.ReleaseField,
                    this.TitleField,
                    this.DescriptionField,
                    this.StatusField,
                    this.FilterCategory);
            }
        }
        private XmlDocument AccuRev(string command, params string[] args)
        {
            return AccuRevPath(null, command, args);
        }
        private XmlDocument AccuRevPath(string workingDirectory, string command, params string[] args)
        {
            var argBuffer = new StringBuilder(command + " ");

            foreach (var arg in args)
                argBuffer.AppendFormat("\"{0}\" ", arg);

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
                throw new InvalidOperationException(Encoding.UTF8.GetString(memoryStream.ToArray()));

            var xmlReader = XmlReader.Create(memoryStream, new XmlReaderSettings() { ConformanceLevel = System.Xml.ConformanceLevel.Fragment });
            var doc = new XmlDocument();
            doc.Load(xmlReader);
            return doc;
        }

        #region Private SchemaInfo Class
        /// <summary>
        /// Contains information parsed from the AccuWork schema.
        /// </summary>
        private sealed class SchemaInfo
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SchemaInfo"/> class.
            /// </summary>
            private SchemaInfo()
            {
            }

            /// <summary>
            /// Gets the ID of the issue ID field.
            /// </summary>
            public int IssueIdFieldId { get; private set; }
            /// <summary>
            /// Gets the ID of the target release field.
            /// </summary>
            public int ReleaseFieldId { get; private set; }
            /// <summary>
            /// Gets the ID of the issue title field.
            /// </summary>
            public int TitleFieldId { get; private set; }
            /// <summary>
            /// Gets the ID of the issue description field.
            /// </summary>
            public int DescriptionFieldId { get; private set; }
            /// <summary>
            /// Gets the ID of the status field.
            /// </summary>
            public int StatusFieldId { get; private set; }
            /// <summary>
            /// Gets the valid issue statuses.
            /// </summary>
            public string[] ValidStatuses { get; private set; }
            /// <summary>
            /// Gets or sets the filter category ID.
            /// </summary>
            public int? CategoryFieldId { get; private set; }
            /// <summary>
            /// Gets or sets the filter category names.
            /// </summary>
            public string CategoryName { get; private set; }
            /// <summary>
            /// Gets the valid filter category values.
            /// </summary>
            public string[] ValidCategoryValues { get; private set; }

            /// <summary>
            /// Parses the specified schema document.
            /// </summary>
            /// <param name="schema">The schema document.</param>
            /// <param name="issueIdName">Name of the issue ID field.</param>
            /// <param name="releaseName">Name of the release field.</param>
            /// <param name="titleName">Name of the title field.</param>
            /// <param name="descriptionName">Name of the description field.</param>
            /// <param name="statusName">Name of the status field.</param>
            /// <param name="categoryNames">Names of categories to filter by.</param>
            /// <returns>Parsed schema field information.</returns>
            public static SchemaInfo Parse(XmlDocument schema, string issueIdName, string releaseName, string titleName, string descriptionName, string statusName, string categoryName)
            {
                var issueIdElement = schema.SelectSingleNode(string.Format("//field[@name='{0}']", issueIdName)) as XmlElement;
                if (issueIdElement == null)
                    throw new ArgumentException(string.Format("Issue ID field '{0}' not found in AccuWork schema.", issueIdName));

                var releaseElement = schema.SelectSingleNode(string.Format("//field[@name='{0}']", releaseName)) as XmlElement;
                if (releaseElement == null)
                    throw new ArgumentException(string.Format("Target Release field '{0}' not found in AccuWork schema.", releaseName));

                var titleElement = schema.SelectSingleNode(string.Format("//field[@name='{0}']", titleName)) as XmlElement;
                if (titleElement == null)
                    throw new ArgumentException(string.Format("Title field '{0}' not found in AccuWork schema.", titleName));

                var descriptionElement = schema.SelectSingleNode(string.Format("//field[@name='{0}']", descriptionName)) as XmlElement;
                if (descriptionElement == null)
                    throw new ArgumentException(string.Format("Description field '{0}' not found in AccuWork schema.", descriptionName));

                var statusElement = schema.SelectSingleNode(string.Format("//field[@name='{0}']", statusName)) as XmlElement;
                if (statusElement == null)
                    throw new ArgumentException(string.Format("Status field '{0}' not found in AccuWork schema.", statusName));

                var validStatuses = new List<string>();
                var validStatusElements = statusElement.SelectNodes("/value");
                if (validStatusElements != null)
                {
                    foreach (XmlElement validStatusElement in validStatusElements)
                        validStatuses.Add(validStatusElement.Value);
                }

                int? categoryId = null;
                string categoryTitle = null;
                var validCategoryValues = new List<string>();
                if (!string.IsNullOrEmpty(categoryName))
                {
                    var chooseFieldElement = schema.SelectSingleNode(string.Format("//field[@name='{0}']", categoryName)) as XmlElement;
                    if (chooseFieldElement == null)
                        throw new ArgumentException(string.Format("Filter category field '{0}' is not defined in AccuWork.", categoryName));
                    if (chooseFieldElement.GetAttribute("type") != "Choose")
                        throw new ArgumentException(string.Format("Only fields defined as type 'Choose' in AccuWork may be used for filtering (field='{0}').", categoryName));

                    categoryId = int.Parse(chooseFieldElement.GetAttribute("fid"));
                    categoryTitle = chooseFieldElement.GetAttribute("label");

                    var validCategoryValueElements = chooseFieldElement.SelectNodes("./value");
                    if (validCategoryValueElements != null)
                    {
                        foreach (XmlElement validCategoryValueElement in validCategoryValueElements)
                            validCategoryValues.Add(validCategoryValueElement.InnerText);
                    }
                }

                return new SchemaInfo()
                {
                    IssueIdFieldId = int.Parse(issueIdElement.GetAttribute("fid")),
                    ReleaseFieldId = int.Parse(releaseElement.GetAttribute("fid")),
                    TitleFieldId = int.Parse(titleElement.GetAttribute("fid")),
                    DescriptionFieldId = int.Parse(descriptionElement.GetAttribute("fid")),
                    StatusFieldId = int.Parse(statusElement.GetAttribute("fid")),
                    ValidStatuses = validStatuses.ToArray(),
                    CategoryFieldId = categoryId,
                    CategoryName = categoryTitle,
                    ValidCategoryValues = validCategoryValues.ToArray()
                };
            }
        }
        #endregion
    }
}
