namespace Contabee.Api.crm;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

    public class CuentaUsuarioResponse
    {
        [JsonPropertyName("direccionesFiscales")]
        public List<DireccionFiscal> DireccionesFiscales { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("usuarioId")]
        public string UsuarioId { get; set; }

        [JsonPropertyName("cuentaFiscalId")]
        public string CuentaFiscalId { get; set; }

        [JsonPropertyName("rfc")]
        public string Rfc { get; set; }

        [JsonPropertyName("claveRegimenFiscal")]
        public string ClaveRegimenFiscal { get; set; }

        [JsonPropertyName("tipoCuenta")]
        public string TipoCuenta { get; set; }

        [JsonPropertyName("fechaAsociacion")]
        public DateTime FechaAsociacion { get; set; }

        [JsonPropertyName("activa")]
        public bool Activa { get; set; }

        [JsonPropertyName("caduca")]
        public bool Caduca { get; set; }

        [JsonPropertyName("estadoLicenciaDemo")]
        public string EstadoLicenciaDemo { get; set; }

        [JsonPropertyName("caducidad")]
        public DateTime? Caducidad { get; set; }
    }

    public class DireccionFiscal
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("cuentaFiscalId")]
        public string CuentaFiscalId { get; set; }

        [JsonPropertyName("entidadFederativa")]
        public string EntidadFederativa { get; set; }

        [JsonPropertyName("municipio")]
        public string Municipio { get; set; }

        [JsonPropertyName("colonia")]
        public string Colonia { get; set; }

        [JsonPropertyName("tipoVialidad")]
        public string TipoVialidad { get; set; }

        [JsonPropertyName("nombreVialidad")]
        public string NombreVialidad { get; set; }

        [JsonPropertyName("numExterior")]
        public string NumExterior { get; set; }

        [JsonPropertyName("numInterior")]
        public string NumInterior { get; set; }

        [JsonPropertyName("codigoPostal")]
        public string CodigoPostal { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("al")]
        public string Al { get; set; }

        [JsonPropertyName("cuentaFiscal")]
        public CuentaFiscal CuentaFiscal { get; set; }
    }

    public class CuentaFiscal
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("cuentaUsuarioId")]
        public string CuentaUsuarioId { get; set; }

        [JsonPropertyName("rfc")]
        public string Rfc { get; set; }

        [JsonPropertyName("compartida")]
        public bool Compartida { get; set; }

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; }

        [JsonPropertyName("apellido1")]
        public string Apellido1 { get; set; }

        [JsonPropertyName("apellido2")]
        public string Apellido2 { get; set; }

        [JsonPropertyName("nacimiento")]
        public string Nacimiento { get; set; }

        [JsonPropertyName("nombreComercial")]
        public string NombreComercial { get; set; }

        [JsonPropertyName("inicioOperaciones")]
        public string? InicioOperaciones { get; set; }

        [JsonPropertyName("estado")]
        public string Estado { get; set; }

        [JsonPropertyName("ultimoCambio")]
        public string UltimoCambio { get; set; }

        [JsonPropertyName("regimen")]
        public string Regimen { get; set; }

        [JsonPropertyName("fechaAlta")]
        public string FechaAlta { get; set; }

        [JsonPropertyName("tipo")]
        public string Tipo { get; set; }

        [JsonPropertyName("claveRegimenFiscal")]
        public string ClaveRegimenFiscal { get; set; }

        [JsonPropertyName("distribuidorId")]
        public string DistribuidorId { get; set; }

        [JsonPropertyName("campanaId")]
        public string CampanaId { get; set; }

        [JsonPropertyName("vendedorId")]
        public string VendedorId { get; set; }

        [JsonPropertyName("regimenIdentificado")]
        public bool RegimenIdentificado { get; set; }

        [JsonPropertyName("activa")]
        public bool Activa { get; set; }

        [JsonPropertyName("estadoLicenciaDemo")]
        public string EstadoLicenciaDemo { get; set; }

        [JsonPropertyName("cuentaUsuario")]
        public object CuentaUsuario { get; set; }

        [JsonPropertyName("direcciones")]
        public List<object> Direcciones { get; set; }

        [JsonPropertyName("mailForwarders")]
        public List<object> MailForwarders { get; set; }

        [JsonPropertyName("tipoCaptura")]
        public string TipoCaptura { get; set; }
    }
