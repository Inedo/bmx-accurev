using System;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.AccuRev
{
    /// <summary>
    /// Custom editor for the AccuWork issue tracking provider.
    /// </summary>
    internal sealed class AccuWorkIssueTrackingProviderEditor : ProviderEditorBase
    {
        private SourceControlFileFolderPicker ffpExePath;
        private ValidatingTextBox txtUserName;
        private PasswordTextBox txtPassword;
        private ValidatingTextBox txtDepot;
        private ValidatingTextBox txtIssueId;
        private ValidatingTextBox txtRelease;
        private ValidatingTextBox txtTitle;
        private ValidatingTextBox txtDescription;
        private ValidatingTextBox txtStatus;
        private TextBox txtClosedStatuses;
        private TextBox txtCategoryFilter;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccuWorkIssueTrackingProviderEditor"/> class.
        /// </summary>
        public AccuWorkIssueTrackingProviderEditor()
        {
        }

        public override void BindToForm(ProviderBase extension)
        {
            EnsureChildControls();

            var accuwork = (AccuWorkIssueTrackingProvider)extension;
            this.ffpExePath.Text = accuwork.ExePath ?? string.Empty;
            this.txtUserName.Text = accuwork.UserName ?? string.Empty;
            this.txtPassword.Text = accuwork.Password ?? string.Empty;
            this.txtDepot.Text = accuwork.DepotName ?? string.Empty;
            this.txtIssueId.Text = accuwork.IssueIdField ?? string.Empty;
            this.txtRelease.Text = accuwork.ReleaseField ?? string.Empty;
            this.txtTitle.Text = accuwork.TitleField ?? string.Empty;
            this.txtDescription.Text = accuwork.DescriptionField ?? string.Empty;
            this.txtStatus.Text = accuwork.StatusField ?? string.Empty;
            this.txtClosedStatuses.Text = string.Join(Environment.NewLine, accuwork.ClosedStatuses ?? new string[0]);
            this.txtCategoryFilter.Text = accuwork.FilterCategory ?? string.Empty;
        }
        public override ProviderBase CreateFromForm()
        {
            EnsureChildControls();

            return new AccuWorkIssueTrackingProvider()
            {
                ExePath = this.ffpExePath.Text,
                UserName = this.txtUserName.Text,
                Password = this.txtPassword.Text,
                DepotName = this.txtDepot.Text,
                IssueIdField = this.txtIssueId.Text,
                ReleaseField = this.txtRelease.Text,
                TitleField = this.txtTitle.Text,
                DescriptionField = this.txtDescription.Text,
                StatusField = this.txtStatus.Text,
                ClosedStatuses = this.txtClosedStatuses.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries),
                FilterCategory = this.txtCategoryFilter.Text
            };
        }
        public override void InitializeDefaultValues()
        {
            base.InitializeDefaultValues();

            EnsureChildControls();

            this.txtIssueId.Text = "issueNum";
            this.txtRelease.Text = "targetRelease";
            this.txtTitle.Text = "shortDescription";
            this.txtDescription.Text = "description";
            this.txtStatus.Text = "status";
            this.txtClosedStatuses.Text = "Closed";
        }

        /// <summary>
        /// Called by the ASP.NET page framework to notify server controls that use composition-based
        /// implementation to create any child controls they contain in preparation for posting back or rendering.
        /// </summary>
        protected override void CreateChildControls()
        {
            base.CreateChildControls();

            this.ffpExePath = new SourceControlFileFolderPicker()
            {
                ServerId = this.EditorContext.ServerId,
                Required = true,
                DisplayMode = SourceControlBrowser.DisplayModes.FoldersAndFiles
            };

            this.txtUserName = new ValidatingTextBox()
            {
                Required = true,
                Width = 300
            };

            this.txtPassword = new PasswordTextBox()
            {
                Width = 270
            };

            this.txtDepot = new ValidatingTextBox()
            {
                Required = true,
                Width = 300
            };

            this.txtIssueId = new ValidatingTextBox()
            {
                Required = true,
                Width = 300
            };

            this.txtRelease = new ValidatingTextBox()
            {
                Required = true,
                Width = 300
            };

            this.txtTitle = new ValidatingTextBox()
            {
                Required = true,
                Width = 300
            };

            this.txtDescription = new ValidatingTextBox()
            {
                Required = true,
                Width = 300
            };

            this.txtStatus = new ValidatingTextBox()
            {
                Required = true,
                Width = 300
            };

            this.txtClosedStatuses = new TextBox()
            {
                Width = 300,
                TextMode = TextBoxMode.MultiLine,
                Rows = 3
            };

            this.txtCategoryFilter = new TextBox()
            {
                Width = 300
            };

            CUtil.Add(this,
                new FormFieldGroup(
                    "Executable File Location",
                    @"The location of the AccuRev command-line executable. On Windows, this is typically C:\Program Files\AccuRev\bin\accurev.exe.",
                    false,
                    new StandardFormField("Executable File Path:", this.ffpExePath)
                    ),
                new FormFieldGroup(
                    "Authentication",
                    "The user name and password BuildMaster will use to log in to AccuRev.",
                    false,
                    new StandardFormField("User Name:", this.txtUserName),
                    new StandardFormField("Password:", this.txtPassword)
                    ),
                new FormFieldGroup(
                    "Depot",
                    "The AccuRev Depot of the issue database to connect to.",
                    false,
                    new StandardFormField("Depot:", this.txtDepot)
                    ),
                new FormFieldGroup(
                    "Required Fields",
                    "AccuWork issue field names as defined in the issue database schema.",
                    false,
                    new StandardFormField("Issue ID:", this.txtIssueId),
                    new StandardFormField("Target Release:", this.txtRelease),
                    new StandardFormField("Issue Title:", this.txtTitle),
                    new StandardFormField("Issue Description:", this.txtDescription),
                    new StandardFormField("Issue Status:", this.txtStatus)
                    ),
                new FormFieldGroup(
                    "Closed Issue Statuses",
                    "Issue statuses which represent a 'closed' issue state (one per line).",
                    false,
                    new StandardFormField(string.Empty, this.txtClosedStatuses)
                    ),
                new FormFieldGroup(
                    "Issue Category Field",
                    "The name of an AccuRev issue field that will be used to filter issues by category.",
                    false,
                    new StandardFormField("Category Field (blank for no filtering):", this.txtCategoryFilter)
                    )
                );
        }
    }
}
