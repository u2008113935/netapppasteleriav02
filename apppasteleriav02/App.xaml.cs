using Microsoft.Maui.Controls;
using apppasteleriav02.Services;

namespace apppasteleriav02
{
    public partial class App : Application
    {
        // Servicio compartido accesible como App.Database
        public static SupabaseService Database { get; } = new SupabaseService();

        public App()
        {
            InitializeComponent();
            MainPage = new AppShell();
        }
    }
}