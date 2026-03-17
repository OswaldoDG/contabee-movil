using ContaBeeMovil.Helpers;
using ContaBeeMovil.PageModels.Camara;

namespace ContaBeeMovil.Pages.Camara;

public partial class TomarFotoPage : ContentPage
{
    public TomarFotoPage(TomarFotoPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;

        try
        {
            this.BackgroundColor = UIHelpers.GetColor("Background");
            LabelHint.TextColor = UIHelpers.GetColor("SecondaryText");
        }
        catch { }
    }
}
