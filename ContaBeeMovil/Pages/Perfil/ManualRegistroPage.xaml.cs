using System;
using System.Collections.Generic;
using ContaBeeMovil.Helpers;
using Microsoft.Maui.Controls;
using Contabee.Api.abstractions;
using Contabee.Api.Crm;

namespace ContaBeeMovil.Pages.Perfil;

public partial class ManualRegistroPage : ContentPage
{
    private readonly IServicioCrm _servicioCrm;

    // Constructor: inicializa componentes visuales y registra manejadores de eventos
    public ManualRegistroPage(IServicioCrm servicioCrm)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;

        // Poblamos el picker de tipo de persona (Física/Moral)
        PickerPersona.Items.Add("Física");
        PickerPersona.Items.Add("Moral");
        PickerPersona.SelectedIndex = 0;

        // Rellenar el picker de régimen según la persona seleccionada
        PopulateRegimen();

        // Eventos: actualizar contador de RFC, cambiar régimen, cancelar y registrar
        EntryRfc.TextChanged += EntryRfc_TextChanged;
        PickerPersona.SelectedIndexChanged += (_, __) => PopulateRegimen();
        BtnCancel.Clicked += async (_, __) => await Navigation.PopModalAsync();
        BtnRegistrar.Clicked += async (_, __) => await Registrar_Clicked();
    }

    // Actualiza la etiqueta que muestra la longitud del RFC mientras el usuario escribe
    private void EntryRfc_TextChanged(object? sender, TextChangedEventArgs e)
    {
        LabelCounter.Text = $"{(e?.NewTextValue?.Length ?? 0)}/13";
    }

    // Llena el picker de regímenes fiscales según si la persona es Física o Moral
    private void PopulateRegimen()
    {
        PickerRegimen.Items.Clear();
        PersonaType sel = PickerPersona.SelectedIndex == 0 ? PersonaType.Fisica : PersonaType.Moral;
        var list = RegimenFiscalProvider.GetRegimenFiscal(sel);
        foreach (var r in list)
            PickerRegimen.Items.Add($"{r.Codigo} - {r.Descripcion}");
    }

    // Valida datos y llama al servicio CRM para registrar la cuenta fiscal mínima
    private async Task Registrar_Clicked()
    {
        try
        {
            // Validación básica del RFC
            var rfc = EntryRfc.Text?.Trim() ?? string.Empty;
            if (rfc.Length < 12 || rfc.Length > 13)
            {
                await DisplayAlert("RFC inválido", "El RFC debe tener 12 o 13 caracteres.", "OK");
                return;
            }

            // Obtener clave de régimen seleccionada si existe
            var regimenSel = string.Empty;
            if (PickerRegimen.SelectedIndex >= 0)
            {
                var selPersona = PickerPersona.SelectedIndex == 0 ? PersonaType.Fisica : PersonaType.Moral;
                regimenSel = RegimenFiscalProvider.GetRegimenFiscal(selPersona)[PickerRegimen.SelectedIndex].Codigo;
            }

            // Construir modelo a enviar al servicio
            var modelo = new CuentaFiscalMinima
            {
                Nombre = EntryName.Text?.Trim() ?? string.Empty,
                Rfc = rfc,
                CodigoPostal = EntryCp.Text?.Trim() ?? string.Empty,
                ClaveRegimenFiscal = regimenSel,
                Compartido = ChkCompartido.IsChecked
            };

            // Obtener servicio CRM desde el contenedor de dependencias (inyectado)
            if (_servicioCrm == null)
            {
                await DisplayAlert("Error", "Servicio CRM no disponible.", "OK");
                return;
            }

            // Llamar al servicio y manejar la respuesta
            var resp = await _servicioCrm.RegistrarCuentaFiscalMinima(modelo);
            if (!resp.Ok)
            {
                await DisplayAlert("Error", resp.Error?.Mensaje ?? "Error al registrar.", "OK");
                return;
            }

            // Notificar éxito y cerrar modal
            await DisplayAlert("Éxito", "Cuenta fiscal registrada.", "OK");
            await Navigation.PopModalAsync();
        }
        catch (HttpRequestException ex)
        {
            await DisplayAlert("Error de conexión", $"No se pudo conectar al servidor: {ex.Message}", "OK");
        }
        catch (TaskCanceledException ex)
        {
            await DisplayAlert("Error de tiempo", $"La solicitud tardó demasiado: {ex.Message}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error inesperado", $"Ocurrió un error: {ex.Message}", "OK");
        }
    }
}
