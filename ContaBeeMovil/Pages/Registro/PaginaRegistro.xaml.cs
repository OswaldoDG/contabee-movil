using Contabee.Pages.Registro;

namespace ContaBeeMovil.Pages.Registro;

public partial class PaginaRegistro : ContentPage
{
	public PaginaRegistro()
	{
		InitializeComponent();
        BindingContext = new RegistroViewModel();
    }
}