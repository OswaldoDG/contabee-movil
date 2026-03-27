using CommunityToolkit.Maui.Views;

namespace ContaBeeMovil.Services.Notifications;



    public partial class CustomToast : Popup
    {
        public CustomToast(string message)
        {
            InitializeComponent();
            MessageLabel.Text = message;
            this.Opened += OnPopupOpened;
        }

        private async void OnPopupOpened(object? sender, EventArgs e)
        {
            Content!.TranslationY = 100;
            Content.Opacity = 0;

            await Task.WhenAll(
                Content.TranslateToAsync(0, 0, 400, Easing.CubicOut),
                Content.FadeToAsync(1, 400, Easing.Linear)
            );

            await Task.Delay(3000);

            await Task.WhenAll(
                Content.TranslateToAsync(0, 100, 300, Easing.CubicIn),
                Content.FadeToAsync(0, 300, Easing.Linear)
            );

            await CloseAsync();
        }

        private async void OnCloseClicked(object? sender, EventArgs e)
        {
            await Content!.FadeToAsync(0, 200);
            await CloseAsync();
        }
    }
