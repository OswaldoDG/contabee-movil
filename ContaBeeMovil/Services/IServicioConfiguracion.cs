using ContaBee.Models.Configuracion;
using System;
using System.Collections.Generic;
using System.Text;

namespace ContaBee.Services;

public interface IServicioConfiguracion
{
    Task<ConfiguracionApp> ObtieneConfiguracion(TipoConfiguracion tipoConfiguracion);
}
