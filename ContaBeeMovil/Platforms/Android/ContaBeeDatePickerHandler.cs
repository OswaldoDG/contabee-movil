using Android.App;
using Android.Content;
using AndroidX.Core.Content;
using Microsoft.Maui.Handlers;

namespace ContaBeeMovil
{
    /// <summary>
    /// Handler de Android para el DatePicker. El diálogo nativo de calendario hereda del
    /// AlertDialog del framework, por lo que ignora los atributos de tema AppCompat y sus
    /// botones Aceptar/Cancelar quedan con texto casi blanco sobre fondo blanco (invisibles).
    /// Aquí coloreamos esos botones por código al mostrarse el diálogo, usando el color de
    /// marca <c>colorBrand</c> (DayNight: ámbar en tema claro, amarillo en tema oscuro).
    /// </summary>
    public class ContaBeeDatePickerHandler : DatePickerHandler
    {
        protected override DatePickerDialog CreateDatePickerDialog(int year, int month, int day)
        {
            var context = Context!;

            var dialog = new DatePickerDialog(context, (o, e) =>
            {
                if (VirtualView != null)
                    VirtualView.Date = e.Date;
            }, year, month, day);

            // Los botones solo existen una vez que el diálogo se muestra.
            dialog.SetOnShowListener(new ColorBotonesListener(context, dialog));

            return dialog;
        }

        private sealed class ColorBotonesListener : Java.Lang.Object, IDialogInterfaceOnShowListener
        {
            private readonly Context _context;
            private readonly DatePickerDialog _dialog;

            public ColorBotonesListener(Context context, DatePickerDialog dialog)
            {
                _context = context;
                _dialog = dialog;
            }

            public void OnShow(IDialogInterface? dialog)
            {
                var colorId = _context.Resources!.GetIdentifier("colorBrand", "color", _context.PackageName);
                if (colorId == 0)
                    return;

                var color = new Android.Graphics.Color(ContextCompat.GetColor(_context, colorId));

                _dialog.GetButton((int)DialogButtonType.Positive)?.SetTextColor(color);
                _dialog.GetButton((int)DialogButtonType.Negative)?.SetTextColor(color);
                _dialog.GetButton((int)DialogButtonType.Neutral)?.SetTextColor(color);
            }
        }
    }
}
