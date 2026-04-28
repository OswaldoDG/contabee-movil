using ContaBeeMovil.Pages.Login;

namespace ContaBeeMovil.Pages.SinConexion;

public partial class PaginaSinConexion : ContentPage
{
    public PaginaSinConexion()
    {
        InitializeComponent();
    }

    private void OnReintentar(object sender, EventArgs e)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            return;

        if (Preferences.Get("TieneSesion", false))
        {
            Application.Current!.Windows[0].Page = App.Services.GetRequiredService<AppShell>();
        }
        else
        {
            var paginaLogin = App.Services.GetRequiredService<PaginaLogin>();
            Application.Current!.Windows[0].Page = new NavigationPage(paginaLogin);
        }
    }
}
