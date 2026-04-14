namespace ContaBeeMovil.Pages.SinConexion;

public partial class PaginaSinConexion : ContentPage
{
    public PaginaSinConexion()
    {
        InitializeComponent();
    }

    private void OnReintentar(object sender, EventArgs e)
    {
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            Application.Current!.Windows[0].Page = App.Services.GetRequiredService<AppShell>();
    }
}
