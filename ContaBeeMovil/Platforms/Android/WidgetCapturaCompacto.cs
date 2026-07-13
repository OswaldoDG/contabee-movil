using Android.App;
using Android.Appwidget;
using Android.Content;
using Resource = ContaBee.Resource;

namespace ContaBeeMovil;

// Variante 1×1 del widget de captura: logo con badge de cámara, tamaño de ícono
// de app. Reusa la lógica de WidgetCaptura — solo cambia el layout.
[BroadcastReceiver(Name = "mx.contabee.app.WidgetCapturaCompacto", Exported = true, Label = "Capturar ticket (1x1)")]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/widget_captura_compacto_info")]
public class WidgetCapturaCompacto : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context == null || appWidgetManager == null || appWidgetIds == null) return;
        foreach (var widgetId in appWidgetIds)
            WidgetCaptura.ActualizarWidget(context, appWidgetManager, widgetId, Resource.Layout.widget_captura_compacto);
    }
}
