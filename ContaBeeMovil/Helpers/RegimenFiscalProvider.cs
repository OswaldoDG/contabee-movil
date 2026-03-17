using System.Collections.Generic;
using System.Linq;

namespace ContaBeeMovil.Helpers;

public enum PersonaType
{
    Fisica,
    Moral,
    Ambos
}

public class RegimenFiscal
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int Orden { get; set; }
    public PersonaType Persona { get; set; }
}

public static class RegimenFiscalProvider
{
    public static List<RegimenFiscal> GetRegimenFiscal(PersonaType? personaTipo)
    {
        var regimenes = new List<RegimenFiscal>
        {
            new RegimenFiscal{ Codigo = "601", Descripcion = "General de Ley Personas Morales", Orden = 0, Persona = PersonaType.Moral},
            new RegimenFiscal{ Codigo = "603", Descripcion = "Personas Morales con Fines no Lucrativos", Orden = 1, Persona = PersonaType.Moral},
            new RegimenFiscal{ Codigo = "605", Descripcion = "Sueldos y Salarios e Ingresos Asimilados a Salarios", Orden = 1, Persona = PersonaType.Fisica},
            new RegimenFiscal{ Codigo = "606", Descripcion = "Arrendamiento", Orden = 1, Persona = PersonaType.Fisica},
            new RegimenFiscal{ Codigo = "607", Descripcion = "Régimen de Enajenación o Adquisición de Bienes", Orden = 1, Persona = PersonaType.Fisica},
            new RegimenFiscal{ Codigo = "608", Descripcion = "Demás ingresos", Orden = 1, Persona = PersonaType.Fisica},
            new RegimenFiscal{ Codigo = "610", Descripcion = "Residentes en el Extranjero sin Establecimiento Permanente en México", Orden = 1, Persona = PersonaType.Ambos},
            new RegimenFiscal{ Codigo = "611", Descripcion = "Ingresos por Dividendos (socios y accionistas)", Orden = 1, Persona = PersonaType.Fisica},
            new RegimenFiscal{ Codigo = "612", Descripcion = "Personas Físicas con Actividades Empresariales y Profesionales", Orden = 0, Persona = PersonaType.Fisica},
            new RegimenFiscal{ Codigo = "614", Descripcion = "Ingresos por intereses", Orden = 1, Persona = PersonaType.Fisica},
            new RegimenFiscal{ Codigo = "615", Descripcion = "Régimen de los ingresos por obtención de premios", Orden = 1, Persona = PersonaType.Fisica},
            new RegimenFiscal{ Codigo = "616", Descripcion = "Sin obligaciones fiscales", Orden = 1, Persona = PersonaType.Fisica},
            new RegimenFiscal{ Codigo = "620", Descripcion = "Sociedades Cooperativas de Producción que optan por diferir sus ingresos", Orden = 1, Persona = PersonaType.Moral},
            new RegimenFiscal{ Codigo = "621", Descripcion = "Incorporación Fiscal", Orden = 0, Persona = PersonaType.Fisica},
            new RegimenFiscal{ Codigo = "622", Descripcion = "Actividades Agrícolas, Ganaderas, Silvícolas y Pesqueras", Orden = 1, Persona = PersonaType.Moral},
            new RegimenFiscal{ Codigo = "623", Descripcion = "Opcional para Grupos de Sociedades", Orden = 1, Persona = PersonaType.Moral},
            new RegimenFiscal{ Codigo = "624", Descripcion = "Coordinados", Orden = 1, Persona = PersonaType.Moral},
            new RegimenFiscal{ Codigo = "625", Descripcion = "Régimen de las Actividades Empresariales con ingresos a través de Plataformas Tecnológicas", Orden = 1, Persona = PersonaType.Fisica},
            new RegimenFiscal{ Codigo = "626", Descripcion = "Régimen Simplificado de Confianza", Orden = 0, Persona = PersonaType.Ambos}
        };

        if (personaTipo == null)
            return regimenes;

        var filtrados = regimenes.Where(r => r.Persona == PersonaType.Ambos || r.Persona == personaTipo.Value).ToList();
        var regimen1 = filtrados.Where(f => f.Orden == 0).OrderBy(f => f.Descripcion).ToList();
        var regimen2 = filtrados.Where(f => f.Orden == 1).OrderBy(f => f.Descripcion).ToList();
        return regimen1.Concat(regimen2).ToList();
    }
}
