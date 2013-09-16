using System;
using Inedo.BuildMaster.Extensibility.Providers.IssueTracking;

namespace Inedo.BuildMasterExtensions.AccuRev
{
    /// <summary>
    /// Represents a category in AccuWork.
    /// </summary>
    [Serializable]
    internal sealed class AccuWorkCategory : CategoryBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccuWorkCategory"/> class.
        /// </summary>
        /// <param name="name">The category name.</param>
        public AccuWorkCategory(string name)
            : base(name, name, null)
        {
        }
    }
}
