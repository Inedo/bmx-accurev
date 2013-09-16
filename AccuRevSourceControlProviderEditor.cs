using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.AccuRev
{
    /// <summary>
    /// Custom editor for the AccuRev source control provider.
    /// </summary>
    internal sealed class AccuRevSourceControlProviderEditor : ProviderEditorBase
    {
        private SourceControlFileFolderPicker ffpExePath;
        private ValidatingTextBox txtUserName;
        private PasswordTextBox txtPassword;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccuRevSourceControlProviderEditor"/> class.
        /// </summary>
        public AccuRevSourceControlProviderEditor()
        {
        }

        public override void BindToForm(ProviderBase extension)
        {
            EnsureChildControls();

            var accurev = (AccuRevSourceControlProvider)extension;
            this.ffpExePath.Text = accurev.ExePath ?? string.Empty;
            this.txtUserName.Text = accurev.UserName ?? string.Empty;
            this.txtPassword.Text = accurev.Password ?? string.Empty;
        }
        public override ProviderBase CreateFromForm()
        {
            EnsureChildControls();

            return new AccuRevSourceControlProvider()
            {
                ExePath = this.ffpExePath.Text,
                UserName = this.txtUserName.Text,
                Password = this.txtPassword.Text
            };
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
                    )
                );
        }
    }
}
