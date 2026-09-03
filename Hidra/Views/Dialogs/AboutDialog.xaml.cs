using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Hidra.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class AboutDialog : UserControl
    {
        public AboutDialog()
        {
            InitializeComponent();
            VersionTextBlock.Inlines.Add(new Bold(new Run($"Version: {GetVersion()}")));
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
        }

        private string GetVersion()
        {
            // Assembly.Location is always empty for a single-file-published app, which would
            // make FileVersionInfo.GetVersionInfo(assembly.Location) throw; reading the
            // AssemblyVersion attribute directly works the same in every publish mode.
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.GetName().Version?.ToString();
        }
    }
}
