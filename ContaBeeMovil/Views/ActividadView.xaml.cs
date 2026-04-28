using ContaBeeMovil.Helpers;

namespace ContaBeeMovil.Views;

public partial class ActividadView : ContentView
{
    public ActividadView()
    {
        InitializeComponent();

        PullRefresh.HandlerChanged += (_, _) => AplicarColorRefresh();

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += (_, _) =>
                MainThread.BeginInvokeOnMainThread(AplicarColorRefresh);
        }
    }

    private void AplicarColorRefresh()
    {
#if ANDROID
        if (PullRefresh.Handler?.PlatformView is AndroidX.SwipeRefreshLayout.Widget.SwipeRefreshLayout swipe)
        {
            var primary = UIHelpers.GetColor("Primary");
            var yellow = new Android.Graphics.Color(
                (byte)(primary.Red * 255),
                (byte)(primary.Green * 255),
                (byte)(primary.Blue * 255));
            swipe.SetProgressBackgroundColorSchemeColor(yellow);
            swipe.SetColorSchemeColors(Android.Graphics.Color.Black);
        }
#elif IOS
        if (PullRefresh.Handler?.PlatformView is UIKit.UIView platformView)
        {
            MakeScrollViewTransparent(platformView);
            var uiRefresh = FindUIRefreshControl(platformView);
            if (uiRefresh != null)
            {
                uiRefresh.BackgroundColor = UIKit.UIColor.Clear;
                uiRefresh.TintColor = UIKit.UIColor.Black;
            }
        }
#endif
    }

#if IOS
    private static UIKit.UIRefreshControl? FindUIRefreshControl(UIKit.UIView view)
    {
        if (view is UIKit.UIScrollView scrollView)
            return scrollView.RefreshControl;

        foreach (var subview in view.Subviews)
        {
            var result = FindUIRefreshControl(subview);
            if (result != null) return result;
        }
        return null;
    }

    private static void MakeScrollViewTransparent(UIKit.UIView view)
    {
        if (view is UIKit.UIScrollView scrollView)
            scrollView.BackgroundColor = UIKit.UIColor.Clear;

        foreach (var subview in view.Subviews)
            MakeScrollViewTransparent(subview);
    }
#endif
}
