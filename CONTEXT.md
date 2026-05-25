# ContaBee Movil — Contexto acumulado

> Este archivo se actualiza al final de cada conversación relevante.  
> Formato: sección más reciente arriba. Incluir fecha, qué se hizo, qué quedó pendiente.

---

## 2026-05-25 — Merge de release1.5 en release2.0 + corrección de conflictos

**Hecho:**
- Merge `release1.5` → `release2.0` para unir todos los módulos en una sola rama.
- Corregidos conflictos de merge en 3 archivos rotos por el merge automático:
  - `Controls/SimpleTabBar.xaml` — reescrito limpio con 5 tabs (Grid de 5 columnas).
  - `Controls/SimpleTabBar.xaml.cs` — eliminados handlers duplicados, `OnTabEquipo_Tapped` corregido a índice 4, `UpdateVisualState` unificado.
  - `Pages/MainTabbedPage.xaml.cs` — eliminada doble inicialización de vistas, `ForceEquipoTab` corregido a tab 4, `case 3`/`case 4` separados correctamente, `ActivarTabActual` sin `case 2` duplicado.
- Verificado que el XAML es válido (error MAUIG1001 era caché de VS — se resuelve con Clean + Rebuild).

**Decisiones tomadas:**
- Estrategia: **merge** en lugar de rebase para ramas compartidas en remoto (evita reescritura de historial).
- Layout de tabs final: 0=Inicio, 1=Facturación, 2=Devoluciones, 3=Comprobaciones, 4=Equipo.
- Tab Equipo se oculta dinámicamente con `SetEquipoVisible(!AppState.EsLoginLess)`.

**Pendiente:**
- Probar en dispositivo/emulador que los 5 tabs navegan correctamente.
- Push de `release2.0` al remoto cuando esté validado.

---

## 2026-05-22 — Configuración inicial de archivos de contexto

**Hecho:**
- Creado `CLAUDE.md` con información estructural del proyecto (stack, arquitectura, convenciones).
- Creado `CONTEXT.md` (este archivo) para acumular contexto entre sesiones.

**Estado del proyecto:**
- Versión actual: 1.0.38 (build 38), rama `release2.0`.
- Último merge: `feature/DevolucionesYComprobaciones` (#46) — funcionalidad de Devoluciones y Comprobaciones integrada.
- Última corrección: UI Android/iOS (`4ff9716`).

**Nota:** Revisión de App Store / Play Store completada y superada. Cuando se agreguen nuevas funciones, generar un nuevo `APP_STORE_REVIEW.md` fresco.

---

## 2026-05-22 — Persistencia de tarjetas en backend (CouchDB)

**Hecho — Backend (`contabee-transcript-backend`, pod identity):**
- Creado modelo `TarjetaUsuario` (`Id`, `Alias`, `UltimosDigitos`) en `contabee.model.identity\usuarios\` — es el DTO que recibe y devuelve la API, sin `UsuarioId`.
- Creado documento CouchDB `TarjetasUsuario` (hereda `CouchDocument`) en `contabee.services.identity\couchdb\` — un documento por usuario, `Id = UsuarioId`, con lista embebida de tarjetas.
- Creado `DbContextTarjetasCouchDb` con base de datos `tarjetas_usuarios`.
- Creado `IServicioTarjetas` + `ServicioTarjetas` — `ObtenerTarjetas` (FindAsync) y `GuardarTarjetas` (find-or-create + AddOrUpdateAsync). Estrategia full-sync: PUT reemplaza toda la lista del usuario.
- Agregado `CouchDB.NET` v3.6.1 y `CouchDB.NET.DependencyInjection` al `.csproj` del services project.
- Registrado `IServicioTarjetas` y `DbContextTarjetasCouchDb` en `Program.cs`.
- Agregada config `couchdb.endpoint/username/password` en `appsettings.Development.json`.
- Endpoints integrados en `UsuariosController` (no controlador separado):
  - `GET /api/identity/usuarios/tarjetas` → devuelve `List<TarjetaUsuario>` del usuario autenticado.
  - `PUT /api/identity/usuarios/tarjetas` → acepta `List<TarjetaUsuario>`, reemplaza todo (full-sync).
- `UsuarioId` siempre se extrae del JWT (claim `sub`) en el controller — el cliente nunca lo envía.

**Hecho — Móvil (`contabee-movil`):**
- Agregado `ToolbarItem` "Copiar JSON" en `TarjetasPage.xaml`.
- Implementado `OnCopiarJsonTapped` en `TarjetasPage.xaml.cs`: serializa la lista de tarjetas proyectando solo `{Id, Alias, UltimosDigitos}` y la copia al portapapeles (para pruebas manuales del endpoint).

**Decisiones tomadas:**
- CouchDB en lugar de MySQL para tarjetas — patrón documento-por-usuario, sin JOIN, escala linealmente.
- DTO sin `UsuarioId` expuesto al cliente — backend lo inyecta desde el token.
- Endpoints en `UsuariosController` (no `TarjetasController` dedicado) — mantiene cohesión de recursos de usuario.
- `IServicioTarjetas` separado de `IServicioUsuarios` — evita mezclar CouchDB y MySQL en un mismo servicio.

**Integración frontend:** completada en sesión posterior (ver entrada siguiente).

---

## 2026-05-22 — Integración frontend de tarjetas con backend

**Hecho:**
- `ServicioSesion.GetTarjetasAsync()` reemplazado: llama al backend (`_servicioIdentidad.MisTarjetasUsuario()`) como fuente de verdad. Si el backend falla, usa `SecureStorage` como fallback con toast de advertencia.
- `ServicioSesion.GuardarTarjetasAsync()` reemplazado: llama al backend (`GuardarMisTarjetasUsuario()`) y siempre actualiza el caché local y `AppState`, independientemente del resultado del backend.
- Migración silenciosa implementada: si el backend devuelve lista vacía pero `SecureStorage` tiene tarjetas, las sube automáticamente (primer login con la nueva versión).
- Helpers privados `ToDto` / `FromDto` agregados al final de `ServicioSesion` para mapear `TarjetaModel` ↔ `Contabee.Api.Identidad.TarjetaUsuario`.
- Alias de tipo `using TarjetaDto = Contabee.Api.Identidad.TarjetaUsuario` usado para evitar colisión con `System.Net.HttpStatusCode` (el namespace generado por NSwag define su propio `HttpStatusCode`).
- `ToolbarItem` "Copiar JSON" y su handler eliminados de `TarjetasPage` (era solo para pruebas del endpoint).
- Correcciones de errores pre-existentes en `Contabee.Api/ServicioIdentidad.cs`:
  - `GuardarMisTarjetasUsuario`: el PUT devuelve `void` — eliminada asignación inválida a `var`.
  - `ConfirmarCuenta`: el backend agregó parámetro `forzar` al endpoint — se pasa `null`.
- Compilación: **0 errores** (solo warnings pre-existentes de nullability y CA1422).

**Decisiones tomadas:**
- `SecureStorage` se mantiene como **caché local** (resiliencia offline), no se elimina.
- Backend es la fuente de verdad; el caché se actualiza en cada GET/PUT exitoso.
- No se modificó `IServicioSesion` — las firmas de `GetTarjetasAsync` y `GuardarTarjetasAsync` son idénticas.
- `PostEliminarCuentaAsync` no requiere cambios — ya limpia el caché local; el backend elimina el documento CouchDB server-side al borrar la cuenta.

**Pendiente:**
- Probar flujo completo en dispositivo/emulador con backend levantado (local o staging).
- Verificar migración: usuario con tarjetas en `SecureStorage` y backend vacío → tarjetas deben aparecer en backend tras primer login.

---

## Plantilla para nuevas entradas

```
## YYYY-MM-DD — Título corto de la sesión

**Hecho:**
- ...

**Decisiones tomadas:**
- ...

**Pendiente / próximos pasos:**
- ...
```
