using ContaBeeMovil.PageModels.Camara;

namespace ContaBeeMovil.Pages.Camara;

public partial class TomarFotoPage : ContentPage
{
    public TomarFotoPage(TomarFotoPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}
