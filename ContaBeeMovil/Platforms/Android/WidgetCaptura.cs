using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using Resource = ContaBee.Resource;

namespace ContaBeeMovil;

[BroadcastReceiver(Name = "mx.contabee.app.WidgetCaptura", Exported = true, Label = "Capturar ticket")]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/widget_captura_info")]
public class WidgetCaptura : AppWidgetProvider
{
    internal const string ExtraWidgetCaptura = "mx.contabee.app.WIDGET_CAPTURA";

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context == null || appWidgetManager == null || appWidgetIds == null) return;
        foreach (var widgetId in appWidgetIds)
            ActualizarWidget(context, appWidgetManager, widgetId);
    }

    private static void ActualizarWidget(Context context, AppWidgetManager appWidgetManager, int widgetId)
    {
        var views = new RemoteViews(context.PackageName!, Resource.Layout.widget_captura);

        var intent = new Intent(context, typeof(MainActivity));
        intent.PutExtra(ExtraWidgetCaptura, true);
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);

        var pendingFlags = Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S
            ? PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pending = PendingIntent.GetActivity(context, widgetId, intent, pendingFlags);
        views.SetOnClickPendingIntent(Resource.Id.widget_root, pending);
        appWidgetManager.UpdateAppWidget(widgetId, views);
    }
}
