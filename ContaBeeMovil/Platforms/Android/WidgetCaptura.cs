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
            ActualizarWidget(context, appWidgetManager, widgetId, Resource.Layout.widget_captura);
    }

    // Compartido con WidgetCapturaCompacto: ambos widgets lanzan la misma acción,
    // solo cambia el layout.
    internal static void ActualizarWidget(Context context, AppWidgetManager appWidgetManager, int widgetId, int layoutId)
    {
        var views = new RemoteViews(context.PackageName!, layoutId);

        var intent = new Intent(context, typeof(MainActivity));
        intent.PutExtra(ExtraWidgetCaptura, true);
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);

        // Immutable existe desde API 23 (minSdk): mismo flag en todas las versiones,
        // y UpdateCurrent refresca el intent si el widget se reconfigura.
        var pending = PendingIntent.GetActivity(context, widgetId, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        views.SetOnClickPendingIntent(Resource.Id.widget_root, pending);
        appWidgetManager.UpdateAppWidget(widgetId, views);
    }
}
