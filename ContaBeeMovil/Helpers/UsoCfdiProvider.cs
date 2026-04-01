namespace ContaBeeMovil.Helpers;

public class UsoCfdi
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public List<string> RegimenReceptor { get; set; } = [];
}

public static class UsoCfdiProvider
{
    private static readonly List<UsoCfdi> _todos =
    [
        new UsoCfdi
        {
            Codigo = "G01", Descripcion = "Adquisición de mercancías.",
            RegimenReceptor = ["601","603","606","612","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "G02", Descripcion = "Devoluciones, descuentos o bonificaciones.",
            RegimenReceptor = ["601","603","606","612","616","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "G03", Descripcion = "Gastos en general.",
            RegimenReceptor = ["601","603","606","612","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "I01", Descripcion = "Construcciones.",
            RegimenReceptor = ["601","603","606","612","620","621","622","623","624","625"]
        },
        new UsoCfdi
        {
            Codigo = "I02", Descripcion = "Mobiliario y equipo de oficina por inversiones.",
            RegimenReceptor = ["601","603","606","612","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "I03", Descripcion = "Equipo de transporte.",
            RegimenReceptor = ["601","603","606","612","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "I04", Descripcion = "Equipo de computo y accesorios.",
            RegimenReceptor = ["601","603","606","612","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "I05", Descripcion = "Dados, troqueles, moldes, matrices y herramental.",
            RegimenReceptor = ["601","603","606","612","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "I06", Descripcion = "Comunicaciones telefónicas.",
            RegimenReceptor = ["601","603","606","612","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "I07", Descripcion = "Comunicaciones satelitales.",
            RegimenReceptor = ["601","603","606","612","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "I08", Descripcion = "Otra maquinaria y equipo.",
            RegimenReceptor = ["601","603","606","612","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "D01", Descripcion = "Honorarios médicos, dentales y gastos hospitalarios.",
            RegimenReceptor = ["605","606","607","608","611","612","614","615","625"]
        },
        new UsoCfdi
        {
            Codigo = "D02", Descripcion = "Gastos médicos por incapacidad o discapacidad.",
            RegimenReceptor = ["605","606","607","608","611","612","614","615","625"]
        },
        new UsoCfdi
        {
            Codigo = "D03", Descripcion = "Gastos funerales.",
            RegimenReceptor = ["605","606","607","608","611","614","615","625"]
        },
        new UsoCfdi
        {
            Codigo = "D04", Descripcion = "Donativos.",
            RegimenReceptor = ["605","606","607","608","611","612","614","615","625"]
        },
        new UsoCfdi
        {
            Codigo = "D05",
            Descripcion = "Intereses reales efectivamente pagados por créditos hipotecarios (casa habitación).",
            RegimenReceptor = ["605","606","607","608","611","612","614","615","625"]
        },
        new UsoCfdi
        {
            Codigo = "D06", Descripcion = "Aportaciones voluntarias al SAR.",
            RegimenReceptor = ["605","606","607","608","611","612","614","615","625"]
        },
        new UsoCfdi
        {
            Codigo = "D07", Descripcion = "Primas por seguros de gastos médicos.",
            RegimenReceptor = ["605","606","607","608","611","612","614","615","625"]
        },
        new UsoCfdi
        {
            Codigo = "D08", Descripcion = "Gastos de transportación escolar obligatoria.",
            RegimenReceptor = ["605","606","607","608","611","612","614","615","620"]
        },
        new UsoCfdi
        {
            Codigo = "D09",
            Descripcion = "Depósitos en cuentas para el ahorro, primas que tengan como base planes de pensiones.",
            RegimenReceptor = ["605","606","607","608","611","612","614","615","625"]
        },
        new UsoCfdi
        {
            Codigo = "D10", Descripcion = "Pagos por servicios educativos (colegiaturas).",
            RegimenReceptor = ["605","606","607","608","611","612","614","615","625"]
        },
        new UsoCfdi
        {
            Codigo = "S01", Descripcion = "Sin efectos fiscales.",
            RegimenReceptor = ["601","603","605","606","607","608","610","611","612","614","615","616","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "CP01", Descripcion = "Pagos.",
            RegimenReceptor = ["601","603","605","606","607","608","610","611","612","614","615","616","620","621","622","623","624","625","626"]
        },
        new UsoCfdi
        {
            Codigo = "CN01", Descripcion = "Nómina.",
            RegimenReceptor = ["605"]
        },
    ];

    /// <summary>
    /// Retorna los usos de CFDI aplicables al régimen fiscal indicado.
    /// Si codigoRegimen es null o vacío, retorna todos.
    /// </summary>
    public static List<UsoCfdi> GetUsoCfdi(string? codigoRegimen = null)
    {
        if (string.IsNullOrEmpty(codigoRegimen))
            return _todos;

        return _todos
            .Where(u => u.RegimenReceptor.Contains(codigoRegimen))
            .ToList();
    }
}
