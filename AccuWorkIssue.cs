using System;
using Inedo.BuildMaster.Extensibility.Providers.IssueTracking;

namespace Inedo.BuildMasterExtensions.AccuRev
{
    /// <summary>
    /// Represents an issue in the AccuWork issue tracking system.
    /// </summary>
    [Serializable]
    internal sealed class AccuWorkIssue : Issue
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccuWorkIssue"/> class.
        /// </summary>
        /// <param name="id">The issue ID.</param>
        /// <param name="status">The issue status.</param>
        /// <param name="title">The issue title.</param>
        /// <param name="description">The issue description.</param>
        /// <param name="release">The target release of the issue.</param>
        public AccuWorkIssue(string id, string status, string title, string description, string release)
            : base(id, status, title, description, release)
        {
        }
    }
}
