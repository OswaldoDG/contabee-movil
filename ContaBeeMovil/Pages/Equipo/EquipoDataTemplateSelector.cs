namespace ContaBeeMovil.Pages.Equipo;

public class EquipoDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate UsuarioVinculadoTemplate { get; set; } = null!;
    public DataTemplate UsuarioPropioTemplate    { get; set; } = null!;

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        => item is EquipoUsuarioItem u && u.EsUsuarioPropio
            ? UsuarioPropioTemplate
            : UsuarioVinculadoTemplate;
}
