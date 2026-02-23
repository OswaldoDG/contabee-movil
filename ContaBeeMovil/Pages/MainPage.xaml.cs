using ContaBeeMovil.Models;
using ContaBeeMovil.PageModels;

namespace ContaBeeMovil.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}