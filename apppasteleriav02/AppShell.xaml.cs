using apppasteleriav02.Views;
namespace apppasteleriav02
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Registrar rutas no incluidas como ShellContent
            Routing.RegisterRoute("cart", typeof(apppasteleriav02.Views.CartPage));
            Routing.RegisterRoute("checkout", typeof(apppasteleriav02.Views.CheckoutPage));
            //Routing.RegisterRoute("login", typeof(LoginPage));
            Routing.RegisterRoute("profile", typeof(apppasteleriav02.Views.ProfilePage));

        }
    }
}
