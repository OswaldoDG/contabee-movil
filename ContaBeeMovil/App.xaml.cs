using ContaBeeMovil.Pages.Login;

namespace ContaBeeMovil
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var tieneSesion = Preferences.Get("TieneSesion", false);

            if (tieneSesion)
            {
                return new Window(new AppShell());
            }

            return new Window(new PaginaLogin());
        }
    }
}
