# ContaBee Movil — Contexto acumulado

> Este archivo se actualiza al final de cada conversación relevante.  
> Formato: sección más reciente arriba. Incluir fecha, qué se hizo, qué quedó pendiente.

---

## 2026-08-06 — Propiedades de usuario (Equipo): modelo de "aplicar al instante"

**Regla de trabajo (vale para todas las sesiones, también en CLAUDE.md):** **Beto compila y corre la app.** Claude entrega el código y dice qué probar; compilar solo para cazar errores de compilación, nunca dar por verificado en dispositivo.

**Hecho** (`Views/PropiedadesUsuarioPopup.*`, `Pages/Equipo/EquipoViewModel.cs`):
- El switch de **Usuario activo** salió del encabezado y bajó a la lista de Propiedades, junto a *Puede Capturar*, con el mismo estilo.
- **Se eliminó el botón Guardar.** Antes convivían dos modelos de guardado en el mismo formulario (activo = instantáneo, captura = diferido) sin nada que lo señalara; juntos en la misma lista eso era indistinguible. Ahora **ambos switches guardan al vuelo**, con indicador en su propia fila (el formulario ya no se oculta ni salta) y reversión + toast si el backend falla. Único botón: **Cerrar**.
- Efecto secundario resuelto: ya no se pueden perder cambios pendientes al desactivar al usuario (antes *Guardar* se deshabilitaba y el cambio de captura quedaba huérfano).
- **Desactivar pide confirmación** (`IServicioAlerta`) por ser destructivo; activar no. Etiqueta de ayuda cuando la fila de captura está bloqueada — antes solo se atenuaba sin explicación.
- **Vocabulario: "colaborador", no "usuario" ni "vínculo".** `SetActivaAsociacion` desactiva **solo la pertenencia a esta cuenta fiscal**; la cuenta de ContaBee del otro usuario sigue funcionando normal. Textos: "Colaborador activo" / "Desactivar colaborador" / "Colaborador desactivado". **No** redactar como si se le desactivara la cuenta al usuario ("vínculo" y "miembro" se probaron y se descartaron). La confirmación se dejó corta a propósito (`¿Desea desactivar a {nombre}?`). ⚠ Ojo: "colaborador" aquí NO se refiere al **crédito de Colaboración** (comprobaciones/devoluciones) — son cosas distintas con nombre parecido.

**Etiqueta de tipo en las tarjetas de Equipo** (`EquipoUsuarioItem.TipoCuentaTexto` + `TemplatePropio`): ahora describe el rol en el equipo, no el tipo de cuenta. Solo 3 casos: `LoginLessCliente` → "Colaborador Login Less", `UsuarioCaptura` → "Captura", resto → "Colaborador". En la tarjeta del **usuario en sesión se quitó la etiqueta** (el badge "Yo" ya lo identifica). Se perdieron a propósito "Propietario", "Empleado" y "Empleado / Cliente".
- ⚠ El valor inicial de *Puede Capturar* se asigna con `_suprimirEventos = true`: sin eso, abrir el popup dispararía un guardado.

**Ocultar capturistas de la lista: se hará en el BACKEND, no en la app.** Se probó un `.Where(u => u.TipoCuenta != UsuarioCaptura)` en `MapearItems` y **se revirtió**: el `Total` lo manda el servidor, así que filtrar del lado del cliente descuadra el contador ("N encontrados" ≠ tarjetas visibles) y deja páginas cortas o vacías. La búsqueda de identidad tampoco expone `TipoCuenta` como propiedad filtrable (acepta `NombreEmail`, `AsociacionActiva`, `PuedeCapturar`). Beto lo excluye desde el endpoint → la UI no cambia.

**Pendiente:** probar en dispositivo el **`AlertaPopup` sobre el popup abierto** (popup anidado — no hay precedente en el proyecto; los demás alerts salen desde una página). Si en Android queda detrás o cierra el popup de propiedades, cambiar a una fila de confirmación en línea dentro de la misma tarjeta.

---

## 2026-08-05 — Horario de captura de ContaBee (aviso en PaginaCaptura)

**Regla:** ContaBee captura Lun-Vie 9:00-18:00 **hora central de México**, salvo feriado. En los **últimos 3 días del mes** el horario aplica siempre (9-18), aunque caiga en sábado, domingo o feriado — es el cierre mensual.

**Hecho:**
- `Services/Horario/` nuevo: `IServicioHorarioCaptura` + `ServicioHorarioCaptura` (regla, cálculo del siguiente día hábil y redacción del mensaje) e `IProveedorFeriados` + `ProveedorFeriadosVacio`. Registrados en `MauiProgram`.
- `PaginaCaptura`, el aviso vive en **tres lugares y ninguno ocupa espacio permanente**:
  1. **Sin fotos** (`MostrarAvisoHorarioAmplio`): ocupa la zona central con la mascota `contabeepet.png` a todo el alto sobrante + píldora "Fuera de horario de captura" + el mensaje. Sustituye al estado vacío "Sin Capturas" (`MostrarEstadoVacio`) — es el mismo hueco.
  2. **Badge de reloj montado en la esquina del botón Enviar** (`MostrarAvisoHorario`); tap → `Views/HorarioCapturaPopup` con la mascota y el mensaje completo. Va medio fuera del botón (`TranslationX=4, Y=-8`) para que su área de toque no se coma la de Enviar, y con fondo `OnPrimary` porque el botón ya es amarillo. Estuvo en el `TitleView` un rato, pero ahí quedaba pegado al badge de créditos y con `SoloCaptura` se veían dos píldoras amarillas iguales.
  3. **Flyout "Quién captura"**: bajo los créditos de Contabee sale `ResumenHorario` ("reanuda lun 9:00"), que es el momento en que el usuario decide. Usa `FueraDeHorario` (sin mirar el crédito activo), no `MostrarAvisoHorario`.
- Texto: *"Las actividades de captura de Contabee reiniciarán {…} y haremos nuestro mejor esfuerzo por tenerlas listas a la brevedad."*
- Las visibilidades se recalculan juntas en `NotificarPanelCentral()`, que hay que llamar donde cambie `TieneCapturas` o el crédito activo — si no, el aviso y el estado vacío se pisan.

**Reorganización de `PaginaCaptura` (mismo cambio):** la pantalla ya venía apretada y en celulares chicos el área de fotos quedaba inservible. Ahora Medio Pago, Tarjeta y **Uso Factura (ancho completo)** quedan siempre visibles; Sólo evidencia, Urgente, Desglosar IEPS y Notas se fueron a un desplegable **"Más opciones"** (`OpcionesAvanzadasVisibles`, colapsado por default, se recuerda en `Preferences`). Cuando está colapsado, `ResumenOpcionesAvanzadas` lista en la cabecera lo que quedó activo ("Evidencia · IEPS") — **no quitar**: son opciones que cambian el CFDI y no deben esconderse.
- El texto adapta el "cuándo": `hoy a las 9:00 a.m.` / `mañana a las…` / `el próximo lunes…` (+ día y mes si cae a más de 6 días).
- Se reevalúa cada minuto con un `IDispatcherTimer` mientras la página está visible (arranca en `OnAppearing`, para en `OnDisappearing`), para que el aviso aparezca/desaparezca al cruzar las 9:00 / 18:00 sin salir de la pantalla.
- Se renombró `Resources/Images/ContabeePet.png` → `contabeepet.png`: Resizetizer exige nombres en minúsculas y **rompía el build de Android**.

**Decisiones tomadas (con Beto):**
- **Solo informa, no bloquea.** Fuera de horario el usuario puede seguir tomando fotos y enviando; el lote entra a la cola. Bloquear el envío arriesgaba tirar el trabajo de capturar.
- **Solo aplica al crédito de Captura** (`UsarCaptura`). En Autoservicio captura el propio usuario, así que el horario de ContaBee no lo afecta y el banner no aparece.
- **Sin feriados por ahora.** `ProveedorFeriadosVacio` devuelve siempre false; la lógica de feriados ya está escrita y probada en `ServicioHorarioCaptura`, solo falta la fuente de datos.
- Hora central resuelta por `TimeZoneInfo` (`America/Mexico_City` → `Central Standard Time (Mexico)` → UTC-6 fijo como último recurso). **No** se usa la hora del dispositivo: puede estar mal configurada o el usuario de viaje.

**Pendiente:**
- **Enchufar el endpoint de feriados** (ya existe en una rama pendiente del backend): implementar `IProveedorFeriados` contra el cliente NSwag y cambiar el registro en `MauiProgram` — no toca ni el servicio ni la página.
- Verificar el aviso en dispositivo. Para forzarlo: activa **Modo Desarrollador** (10 taps a la versión en Acerca de) y toca el título "Captura" — cada tap recorre los modos de `SimulacionesHorario` (hora real / sábado 11:00 / hoy 21:00 / hoy 07:00 / último día del mes) y escribe `ServicioHorarioCaptura.MomentoSimuladoCentral`. Se guarda en `Preferences`. **No** está tras `#if DEBUG` a propósito: las pruebas son en celular con builds normales, donde ese símbolo no existe; el gate real es `AppState.EsDev`.
- `PuedeSerUrgente` sigue usando `DateTime.Now.Hour < 20` (hora **del dispositivo**, no central). Quedó fuera de alcance, pero es la misma clase de bug que este cambio evita.
- **Nada de esta sesión pasó por el compilador**: el `obj\` de Android estuvo bloqueado por el IDE (`banditoth.MAUI.DeviceId.dll` en uso) toda la sesión. Solo se validó que los XAML parsean como XML bien formado. El único error de compilación que sí salió y se corrigió: `ShowPopupAsync` vive en `CommunityToolkit.Maui.Extensions`, no en `.Views`.
- **Las preferencias `captura_*` son por dispositivo, no por usuario.** `Preferences` es de la instalación y **nadie llama a `LimpiarPreferencias()`** en el logout (existe en `ServicioAlmacenamiento` sin un solo llamador). Si otro usuario entra en el mismo celular hereda tarjeta, notas, forma de pago y las banderas de CFDI del anterior — no solo el estado del desplegable. Preexistente, no lo introdujo este cambio. Arreglo propuesto y **no** implementado (Beto lo dejó pasar por ahora): prefijar las claves con el id de usuario (`captura_tarjeta_id__{userId}`), preferible a limpiar todo en el logout porque eso también borraría modo dev, versión ignorada y caché del dashboard.

---

## 2026-08-04 — "Buscar actualizaciones" manual en Acerca de

**Hecho:**
- `IServicioActualizacion.VerificarManualAsync()` (nuevo) + enum `ResultadoChequeoManual` (`HayActualizacion` / `Actualizado` / `SinConexion` / `NoDisponible`). El núcleo común quedó en `ConsultarYAvisarAsync(respetarIgnorada)`, que ahora comparte con `VerificarAsync`; se llama con el `SemaphoreSlim` ya tomado.
- Diferencias del chequeo manual vs. el automático: **no** aplica el throttle diario ni respeta `Actualizacion_VersionIgnorada` (si el usuario lo pide, se consulta y se avisa siempre), y **devuelve** resultado para poder responder también cuando NO hay actualización. Sí sigue registrando `Actualizacion_UltimoChequeo` y sí sigue guardando el "Ahora no".
- `AcercaDePage`: tarjeta nueva "Buscar actualizaciones" (`arrow_sync_20_filled`) justo bajo la de versión, con loader de puntos reusando el patrón de Servicios/Carga actual y guard `_verificandoActualizacion` contra doble tap. Con actualización disponible el aviso lo muestra el servicio; el toast de la página solo cubre los casos sin popup (al día / sin conexión / no disponible).

**Decisiones tomadas:**
- Si ya hay un chequeo en curso (o un aviso en pantalla) el manual sale con `NoDisponible` en lugar de esperar — el `_gate` se toma con `WaitAsync(0)`, igual que el automático, para no apilar popups.
- En Windows/MacCatalyst el chequeo devuelve `NoDisponible` (el backend solo conoce Android e Ios). No se ocultó el botón por plataforma.

**Pendiente:** probar en dispositivo (Android/iOS): al día, con versión nueva, y en modo avión.

---

## 2026-07-31 — Aviso de actualización adaptado al contrato final del backend + botón de prueba

**Contexto:** el backend ya deployó el control de versión, pero con un contrato **distinto** al que la app asumía. `GET /app/version-movil` (identity, `[AllowAnonymous]`) ya **no** devuelve `versionMinima` / `versionRecomendada`, solo `versionActual` + urls. La comparación ahora la hace el servidor en un endpoint nuevo. Con el contrato viejo el aviso **nunca se hubiera mostrado** (ambas versiones llegaban null → salía por el `return`).

**Hecho** (`Services/ServicioActualizacion.cs`, `AppShell.xaml` + `.xaml.cs`):
- **Nuevo endpoint:** `GET /api/identity/app/version-movil/verifica?version={mayor.menor.parche}&plataforma={Android|Ios}`. Se manda `AppInfo.Current.Version` recortada a 3 componentes (AppInfo puede traer 4). Respuesta: `requiereActualizacion` (único campo que decide), `urlActualizacion`, `versionActual`, `mensaje`.
- **400 = no actualizar, seguir de largo.** Pasa naturalmente en builds internos/TestFlight adelantados a lo publicado, y en versión no parseable. Se loguea y se trata como "no hay update"; **no** cae al fallback de tienda.
- **Se eliminó el concepto de versión obligatoria** (`_obligatoriaPendiente`, `VersionMinima`, popup sin botón cancelar): el backend ya no lo expone, el aviso es informativo y no bloqueante. Si más adelante reintroducen "versión mínima", hay que volver a agregarlo.
- **Plataformas no contempladas** (Windows / MacCatalyst): se omite el chequeo, el backend solo conoce `Android` e `Ios`.
- Fallback a tienda (iTunes lookup / scrape de Play) **se conserva** pero solo para cuando el backend no responde (red caída, 5xx); ahora hace la comparación local y produce el mismo `ResultadoVersion`.
- DTO local renombrado a `RespuestaVerificacion` y **anidado privado** dentro del servicio: NSwag ya genera `VerificacionVersionMovilRespuesta` y `VersionMovilRespuesta` en `Contabee.Api.Identidad` y chocaban por nombre.
- Throttle diario, ignore con caducidad de 3 días y guard `SemaphoreSlim` se mantienen igual; la clave `Actualizacion_VersionIgnorada` ahora guarda `versionActual` del servidor.
- Hubo un botón dev en el menú lateral para forzar el aviso; se **eliminó** una vez validada la apariencia en dispositivo (junto con `MostrarAvisoDemoAsync`). Recuperable del historial si se vuelve a necesitar.

**Apariencia — popup dedicado `Views/ActualizacionPopup.xaml`:** el aviso ya no usa `AlertaPopup`. Decisión deliberada: `AlertaPopup` es el alert genérico de toda la app (cerrar sesión, confirmaciones, errores) y no debe cargar con la identidad visual de esta pantalla. Diseño "hero de marca" elegido por Beto: banda superior `Primary #fec001` de 118px con dos elipses blancas decorativas (opacidad 0.16/0.12) y el ícono `arrow_download_24_filled` en círculo blanco de 72px; cuerpo con `Title3` + `Body2`; píldora `Alternate` con **instalada → nueva** (`1.0.38 → 2.5.6`); `PrimaryButton` a ancho completo y **"Ahora no" como texto plano** (no botón) para que la acción secundaria no compita.
- La píldora se **oculta** si falta alguna de las dos versiones (fallback a tienda / backend sin configurar).
- **Tocar fuera ≠ "Ahora no":** el popup expone `Confirmado` y `Pospuesto`; solo el "Ahora no" explícito persiste `Actualizacion_VersionIgnorada` (3 días). Cerrar tocando afuera no silencia nada — el throttle diario ya evita que insista ese día.
- En el aviso de muestra (botón dev), si `versionActual` del backend coincide con la instalada se enseña la instalada +1 en el parche, para que la píldora no se vea `1.0.38 → 1.0.38`.
- `ServicioActualizacion` ya **no** depende de `IServicioAlerta`; monta el popup directo con el guard de `PaginaSinConexion` que tenía `ServicioAlerta`.

**Trampa de `MauiXamlInflator=SourceGen` (csproj:28) — leer antes de agregar XAML nuevo.** Al correr el popup por primera vez reventó con `XamlParseException: No embeddedresource found for __XamlGeneratedCode__.__Type<hash>`. Causa: el inflador SourceGen compila el XAML a C# y **no lo embebe como recurso**; cuando XAML Hot Reload detecta el archivo editado intenta inflarlo en runtime (`LoadFromXaml`) y no encuentra el recurso. No es defecto del XAML — le pega a cualquier archivo editado en sesión de depuración. Solución aplicada: `<MauiXaml Update="Views\ActualizacionPopup.xaml" Inflator="Default" />` (Runtime en Debug → Hot Reload funciona; XamlC en Release). Ya había precedente con `AppStyles.xaml` (`Inflator="Runtime"`). Verificado: con `Inflator="Default"` deja de generarse el `.xsg.cs` del archivo.

**Estado:** probado en dispositivo por Beto — aprobado. Pendiente menor: revisar en tema claro/oscuro que el hero amarillo se recorte bien en las esquinas redondeadas (el clipping del `Border` puede variar por plataforma).

---

## 2026-07-17 — Material Symbols como fuente propia + eliminación de MauiIcons (NuGet)

**Contexto:** el jefe pidió 3 íconos de Material (`receipt`, `price_check`, `payment_arrow_down`) para las tabs de Facturación/Comprobaciones/Devoluciones; MauiIcons (NuGet) tenía historial de fallas ("tofu") y ya casi no se usaba.

**Hecho:**
- **Material Symbols integrado igual que FluentUI:** `Resources/Fonts/MaterialSymbols-Regular.ttf` (FILL=0) y `MaterialSymbols-Filled.ttf` (FILL=1), generados como instancias estáticas (~1.4 MB c/u) desde la fuente variable oficial (10.6 MB) con `fontTools.varLib.instancer` (wght=400, GRAD=0, opsz=24). Nombre interno de cada ttf renombrado ("Material Symbols" / "Material Symbols Filled") para que iOS no las pise entre sí. Clases `MaterialSymbols.cs` / `MaterialSymbolsFilled.cs` (4,266 constantes c/u, generadas del `.codepoints` oficial; dígito inicial → prefijo `_`, keywords C# → `@`). Registradas en `MauiProgram.cs`.
- **Alcance final: MaterialSymbols SOLO en los 3 tabs pedidos.** Facturación → `receipt`, Devoluciones → `payment_arrow_down`, Comprobaciones → `price_check` (variante filled), en `SimpleTabBar` (el activo) y también en `CurvedTabBar` (control huérfano, ninguna página lo usa — se migró por consistencia, no se eliminó por si se retoma). Inicio y Equipo siguen en FluentUIFilled.
- **MauiIcons eliminado por completo** (3 PackageReference + entradas en `MtouchInterpreter`, `UseMaterialMauiIcons()`, 42 `xmlns:mi` huérfanos, ~15 `using MauiIcons.*`). Los 2 usos reales que tenía MauiIcons (botones de cámara de `PaginaCaptura`, palomita de `SelectorFlotante`) se migraron a **FluentUIFilled** (`camera_20_filled` / `checkmark_20_filled`), no a MaterialSymbols — Beto pidió mantener MaterialSymbols acotado solo a los 3 tabs mientras se evalúa. Converters `TipoProcesoCapturaIconConverter` y `ClaveFormaPagoIconConverter` **eliminados** (estaban declarados como resources en `LoteCapturaCardView` pero ningún binding los usaba). Warm-up de fuentes en `App.xaml.cs` actualizado (incluye "MaterialSymbols"/"MaterialSymbolsFilled" junto a las FluentUI).
- Regenerar/subsetear: el proceso quedó en `build_material.py` (scratchpad de la sesión, no en repo); la fuente origen es github.com/google/material-design-icons `variablefont/`.

**Decisiones tomadas:**
- Se embarca el set completo (~2.8 MB total) y no un subset, a petición de Beto: si tras probar le gusta, planea migrar TODOS los íconos de la app a Material Symbols.
- Hallazgo: el "tofu" histórico de MauiIcons NO era falta de registro (`UseMaterialMauiIcons()` sí existía); causa nunca confirmada.

**Pendiente / próximos pasos:**
- Beto compila y prueba en dispositivo (el primer build con MaterialSymbols compiló verde; tras quitar MauiIcons el build local falló solo por lock de `obj\` del IDE, no por código).
- Posible migración total FluentUI → Material Symbols si convence.

---

## 2026-07-15 — Fix Tienda/IAP: rename `pasarelarPago` → `pasarelaPago` del backend

**Causa:** commit `81bf5e9` del backend ("Ajustes gestion de carritos en portal", PR #81) renombró en `DtoCreaCarritoCompras` (clase base del `DtoComprobanteCompra` que reciben `/iappurchases/verificar` y `/iappurchases/completar`) la propiedad `PasarelarPago` → `required PasarelaPago`. Doble ruptura: cambia el nombre JSON **y** al ser `required` en System.Text.Json un body sin `pasarelaPago` falla la deserialización → 400 → la app lo lee como "compra no válida".

**Hecho:**
- Swagger ya venía actualizado en el working tree; el cliente NSwag **se genera en build** (`OpenApiReference` → `obj/ServicioEcommerceClient.cs`, no se commitea), así que "regenerar" = compilar. Solo faltaba adecuar los call sites hechos a mano.
- Renombrado `PasarelarPago` → `PasarelaPago` (enum + propiedad) en `Contabee.Api/ServicioEcommerce.cs` y `Pages/Tienda/TiendaPage.xaml.cs` (ambos bloques: `EnviarAlBackendYCompletarAsync` y `ProcesarCompraDirectaAsync`, en las 3 ramas `#if IOS/ANDROID/#else`). Cero referencias viejas restantes.
- Verificado el contrato de salida en el cliente generado: `[JsonProperty("pasarelaPago", Required = Always)]` + `StringEnumConverter` → manda `"pasarelaPago":"Apple"|"Google"`, que es lo que el back espera.
- Build OK en `Contabee.Api`, `net10.0-android` y `net10.0-ios`.

**Segundo bug (silencioso, de dinero) — precios por pasarela.** Al auditar los 4 swagger se encontró que `DtoPrecioProducto` ganó la propiedad `pasarela`: el catálogo ya **NO** devuelve un precio Público por producto sino **cuatro**, uno por pasarela y con importes distintos (`CAPTURA15`: Interbancario $80 / MercadoPago $90 / Apple $90 / Google $90). La app hacía `Precios.First(pr => pr.Tipo == Publico)`, que como Interbancario va primero en el arreglo **siempre tomaba el precio Interbancario** → el `MontoCompra` que se persiste quedaba por debajo de lo que realmente cobró la tienda, en **el 100% de los productos**. No lo causó regenerar el cliente (la app vieja ya recibía los 4 precios e ignoraba el campo); regenerar es lo que dio el campo para arreglarlo.
- Fix: constante `TiendaPage.PasarelaPlataforma` (`#if IOS→Apple / ANDROID→Google / else→Interbancario`), usada para elegir el precio (`Tipo == Publico && Pasarela == PasarelaPlataforma`, con fallback al primer Público para no degradar a 0) tanto en el `MontoCompra` como en el display de respaldo. De paso elimina el mapeo plataforma→pasarela que estaba triplicado.
- Verificado contra el catálogo real (`GET /api/ecommerce/categorias/full` responde **anónimo**, útil para diagnosticar sin token).

**Otros hallazgos:**
- El swagger renombró endpoints de carrito (`/carrito/cuentafiscal/{id}` → `/carrito/pendientes/{cfid}`, nuevos DELETE `/carrito/{carritoId}` y POST `/carrito/cuentafiscal`), pero la app **no los usa** (solo `Full`, `Verificar`, `Completar`, `Cupones`, `Aplicar`) → sin impacto. `distribuidorId` (CRM/Transcript) y `cuponBienvenida` (Identidad) son aditivos.
- `MotivoEstado` ganó `ErrorPortal`; `UsoCfdiOErrorConverter` tiene arm `_ => "Error"` → no truena, pero muestra mensaje genérico. Falta agregarle su texto.
- ✅ **`paginado` — regresión confirmada y corregida (era un parche manual perdido).** El `nullable: true` de `paginado` NO lo genera el backend: se agrega **a mano** al swagger (lo hizo `cbf0e4d`, 20-mar-2026). La re-descarga de julio lo borró de los **5** lugares que lo tenían en Transcript. Sin él NSwag genera `Required.DisallowNull` y Newtonsoft **truena al deserializar** si el back responde `"paginado": null` → listados caídos. Restaurados los 5 + parchados 4 que **nunca** lo tuvieron (`BusquedaCaptura` en Transcript; `Busqueda`, `CuentaClienteResultadoPaginado`, `CuentaUsuarioResultadoPaginado` en Identidad — este último lo usa `EquipoViewModel`). **9/9 parchados.** Probado: el único efecto en el generado es `Required.DisallowNull` → `Required.Default` (relajación pura, no puede romper nada). Procedimiento y check automatizado quedaron documentados en **CLAUDE.md** para que no se vuelva a perder.
- ⚠ **Trampa de depuración:** `TiendaPage.xaml.cs` loguea el payload con `System.Text.Json.JsonSerializer.Serialize(comprobante)`, que ignora los atributos de Newtonsoft → el log muestra `"PasarelaPago":2` mientras el wire real manda `"pasarelaPago":"Apple"`. El log **no** refleja lo que se envía; no fiarse de él al diagnosticar.

**Pendiente:**
- Probar compra real en dispositivo (Android/TestFlight) — el build verde no prueba que acredite.
- **Reportar al backend precios que se ven mal capturados:** `AUTOSERVICIO50` Google **$1510** vs Apple $150 (10x, dedazo); `COLABORACION250` Apple $1220/Google $1199 vs Interbancario $575 (idénticos a los de `CAPTURA250`, parecen copy-paste); `BIENVENIDA15` $2349 (igual que `CAPTURA500`).
- Sigue abierta la **auditoría de IAP** (12 hallazgos, varios críticos de dinero real: `/completar` no revalida el recibo contra la tienda, sin manejo de reembolsos S2S). Uno de ellos (el backend debe ignorar el `MontoCompra` del cliente y tomar el precio server-side) haría redundante el fix de precios, pero no lo sustituye mientras no exista. Está previsto rehacerla y corregir lo reportado. **El documento NO vive en el repo a propósito** (`docs/` está en `.gitignore`: detalla vulnerabilidades sin corregir); pídeselo a Beto.

---

## 2026-07-13 — Visor PDF: overlay de descarga, título oculto y fix tamaño iOS

**Hecho:**
- Nuevo `Views/CargandoPopup` (toolkit `Popup` con spinner + scrim, `PageOverlayColor #66000000`, no descartable). En `LoteCapturaCardView.DescargarYCompartir` se muestra a pantalla completa **mientras se descarga** el archivo (se conserva además el mini-spinner del botón vía `SetBusy`), y se cierra antes de navegar al visor o abrir la hoja de compartir. Aplica a los 3 botones (PDF/XML/cámara). Cierre robusto: en `finally` best-effort, sin doble cierre.
- `PaginaVisorPdfPropio.Titulo`: ya NO asigna `Title` (solo guarda `_nombreArchivo` para guardar/compartir). El nombre de las capturas es un número (`captura_12345.pdf`) y se veía raro en la barra superior; ahora queda vacía (se conserva el botón de regreso).
- **Fix documento diminuto en iOS (causa raíz REAL, resuelta y verificada en dispositivo).** Tres capas, en orden de descubrimiento:
  1. *Ancho de despliegue:* se calculaba `MainDisplayInfo.Width / Density` (en iOS `Width` viene en puntos → re-dividir por densidad dejaba ⅓ del tamaño). Ahora `_anchoBaseDips` sale del ancho REAL del lienzo vía `Lienzo.SizeChanged` + debounce (siempre DIPs); `_anchoPxObjetivo = ancho × densidad × Multiplicador` (densidad solo como nitidez). Confirmado por log: `ancho=390dips densidad=3`.
  2. *Frame de la página:* cada página se envuelve en un `Grid` de tamaño fijo (`WidthRequest`/`HeightRequest`) y el `Image` lo rellena. En iOS un `Image` con fuente async (`FromStream`) + `AspectFit` dentro de un stack auto-medido no aplica de forma fiable su propio tamaño (el frame colapsa). `ActualizarPill` ahora itera los contenedores (`View`), no `Image`.
  3. **La verdadera causa:** en `ServicioRenderPdf` (iOS) `GetDrawingTransform` hacia un rect **en píxeles** NO ampliaba la página → se dibujaba a ~1:1 en puntos, diminuta dentro de un lienzo grande (blanco sobre fondo blanco = parecía flotar). Fix: `ScaleCTM(ancho/anchoPt)` explícito puntos→píxeles y `GetDrawingTransform` mapea solo a un rect del tamaño visual **en puntos** (conserva el `/Rotate` intrínseco). Diagnóstico: se pintó el fondo del render de azul y se confirmó que el contenido quedaba diminuto rodeado de azul. Android intacto (su `PdfRenderer` ya escala al bitmap).

**Pendiente:** probar en iOS el resto del visor (zoom/pan, rotar ±90°, multipágina/pill, restaurar); descarga lenta → overlay → visor con spinner; compartir PDF/XML; caso sin conexión.

---

## 2026-07-11 — Widget Android "Capturar ticket": correcciones + rediseño (2 widgets)

**Correcciones al flujo** (probadas en dispositivo; flujo: widget → `MainActivity` → `DeepLinkHandler.NavegarACaptura` → `PaginaCaptura` con `tipo=FacturaIndividual`):
- `MainActivity.HandleIntent(intent, desdeOnCreate)`: consume el extra (`RemoveExtra`, evita re-disparo al `Recreate()`) y descarta la re-entrega desde Recientes (`LaunchedFromHistory`) **solo en OnCreate** — ⚠ hallazgo: algunos launchers marcan ese flag también en taps legítimos vía `OnNewIntent`; filtrarlo ahí rompe el widget en caliente.
- `DeepLinkHandler.NavegarACaptura`: sin sesión conserva la intención como link pendiente y `CoordinadorSesion.NavegarAsync(AppShell)` la reprocesa tras login (`ProcesarLinkPendiente()`). Espera de `Shell.Current` con reintentos (20×250ms). Guard anti-duplicados si `PaginaCaptura` ya es la página actual.
- `FacturacionPage.TieneCreditos`: considera créditos Captura **o** Autoservicio (antes solo Captura).
- `PaginaCaptura`: fix fuga — suscripción a `AppState.PropertyChanged` movida de lambda-en-constructor a handler en `OnAppearing`/`OnDisappearing`.
- `PendingIntentFlags.UpdateCurrent|Immutable` unificado (minSdk 23).

**Rediseño — ahora hay 2 widgets** (elegidos por el usuario de una galería dev de 6 propuestas, ya eliminada):
- `WidgetCaptura` (3×1, redimensionable): card con logo + badge de cámara (círculo oscuro, cámara amarilla, aro blanco), "Capturar ticket" / "Toca y fotografía tu ticket", chevron. **Temática**: card blanca (values) / oscura #1e1e1e (values-night). Bloque de logo 42dp para caber en 1 celda sin recorte.
- `WidgetCapturaCompacto` (1×1, nuevo provider): logo 48dp + badge + etiqueta "Capturar" blanca con sombra; fijo en ambos temas. Reusa `WidgetCaptura.ActualizarWidget(…, layoutId)`.
- Recursos: `widget_card`/`widget_badge`/`ic_widget_chevron`/`ic_widget_camera` (amarillo), colores `widget_*` renovados en values/values-night, `previewLayout`+`description` en ambos providers (strings.xml).

**Decisiones vigentes:**
- Widget sin créditos → abre igual (la página muestra "Sin créditos" y bloquea envío).
- Widget sin sesión → retoma la intención y abre captura tras el login.

---

## 2026-07-10 — Visor de PDF de capturas: visor PROPIO (único y definitivo)

**Contexto:** esta app **solo corre en Android e iOS** (los TFM de Windows quedan solo para dev). El visor replica el de la app de escritorio: documento a pantalla completa sobre fondo oscuro (#141414) y botones flotantes cuadrados redondeados (Primary/OnPrimary, íconos **FluentUIFilled**). Historia: se probó PDF.js en HybridWebView (no funcionó en dispositivo) y `Eightbot.MauiNativePdfView` (funcionaba pero sin rotación) — **ambos eliminados**; queda solo el visor propio.

**Hecho:**
- **Visor propio** (`Pages/VisorPdf/PaginaVisorPdfPropio`, abierto desde el ícono de cámara de `LoteCapturaCardView` cuando el contenido es PDF por magic bytes `%PDF`): servicio `Services/Pdf/IServicioRenderPdf`/`ServicioRenderPdf` (singleton DI) renderiza páginas a JPEG con APIs del sistema — `android.graphics.pdf.PdfRenderer` (Android) y `CoreGraphics.CGPDFDocument` + `GetDrawingTransform` (iOS) — sin dependencias; rotación del usuario (0/90/180/270) en código compartido con SkiaSharp (patrón `NormalizarOrientacionExif`). Render a ~2.5× ancho de pantalla (tope 4096 px/lado), fondo blanco explícito, `Task.Run` + cancelación en `OnNavigatedFrom`.
- **Interacción por transforms, sin ScrollView** (el ScrollView se comía el pinch): un `Grid` "Lienzo" opaco recibe TODOS los gestos (pinch + pan + doble tap; el contenedor de páginas es `InputTransparent` con cascade) y el zoom/paneo se aplican como `Scale`/`Translation` sobre el contenedor con clamps (`MaxTx/MaxTy`); zoom alrededor del centro (la traslación escala con el factor relativo); posición inicial = inicio del documento (`_ty = MaxTy()`).
- Controles: zoom ± (paso 1.25, 1–4), pinch (en vivo), doble tap 1x/2x, restaurar (zoom 1 + rotación 0), rotar ±90° (re-render sub-segundo), pill "n / N" (calculada desde la traslación), descargar (`FileSaver`) y compartir (siempre el PDF original).
- **Eliminado el visor nativo**: página `PaginaVisorPdf`, ruta, botón ⇄, paquete `Eightbot.MauiNativePdfView` y `UseMauiNativePdfView()`; **minSdk Android revertido 24 → 23** (el 24 era exigencia de los AAR de ese paquete). CLAUDE.md actualizado.

**Decisiones tomadas:**
- Visor propio elegido tras evaluación en dispositivo; los PDFs son ligeros y de resolución acotada (los genera la app: foto→PDF 1 página con OpenCV/normalización), así que re-renderizar al rotar es barato.
- Recordatorio: capturas viejas (JPEG, pre poc/pdf) comparten en vez de abrir visor; probar con capturas recientes.

**Pendiente / próximos pasos:**
- Probar en dispositivo (equipo compila/corre; sin builds de Claude por locks de `obj\`): pinch/zoom ±/doble tap, paneo, rotar ×4, restaurar, descargar, compartir, multipágina (pill), PDF corrupto (toast+regreso), y en iOS validar el render (PDFKit/CG).
- Si se desea, reusar el visor para el botón PDF del CFDI (`BtnPdf`): mismo bloque condicional en `DescargarYCompartir`.

---

## 2026-07-06 — Aviso de nueva versión disponible (backend-driven)

**Hecho:**
- Nuevo `Services/ServicioActualizacion.cs` (`IServicioActualizacion`, singleton en DI): al arrancar (`App.OnStart`, fire-and-forget con delay de 3s) consulta `GET https://api.contabee.mx/api/identity/app/version-movil` y compara contra `AppInfo.Current.Version`:
  - `< versionMinima` → popup **obligatorio** (solo botón "Actualizar").
  - `< versionRecomendada` → popup "Ignorar / Actualizar" vía `IServicioAlerta`. "Ignorar" guarda la versión en `Preferences` (`Actualizacion_VersionIgnorada`) para no repetir el aviso de esa misma versión.
  - "Actualizar" abre la tienda (`Launcher`): URLs del backend con fallback a Play Store / App Store (`id6761437536`).
- Contrato de respuesta: `{ versionMinima, versionRecomendada, urlAndroid, urlIos, mensaje }` (todo opcional; `mensaje` reemplaza el texto default del popup).
- **Fallback provisional a tiendas** mientras el backend regrese 404: iOS consulta iTunes Lookup (verificado funcionando); Android scrapea la versión del HTML de Play Store. El fallback solo produce aviso "amable", nunca obligatorio. El backend siempre tiene prioridad.

**Decisiones tomadas:**
- **El endpoint del backend AÚN NO EXISTE ni se deployará junto con este cambio.** El servicio falla en silencio, así que el front puede publicarse ya; cuando el back exponga el endpoint la feature se activa sola sin nueva release.
- **Hallazgo:** la ficha de Play Store de `mx.contabee.app` regresa 404 (app en testing cerrado) → el fallback Android no hará nada hasta que la ficha sea pública; iOS sí funciona desde ya.

**Pendiente / próximos pasos:**
- Implementar el endpoint en el pod identity del backend (spec entregada: controller `[Route("app")]`, `GET version-movil`, `[AllowAnonymous]`, valores desde configuración).
- Probar en dispositivo cuando el endpoint exista (incluye caso obligatorio).

---

## 2026-06-26 — Fix: listados con datos de la cuenta fiscal anterior al cambiar de cuenta

**Bug:** Al cambiar de cuenta fiscal, las pestañas de listado (Facturación, Devoluciones, Comprobaciones) seguían mostrando los resultados de la cuenta anterior (son singletons embebidos en `MainTabbedPage` que cachean resultados).

**Hecho:**
- `MainTabbedPage`: reacciona a la transición de `AppState.EstaActualizandoCF` `true → false` → invalida el caché de las 3 páginas (`InvalidarConsulta()`) y reactiva la pestaña visible.

**Decisiones tomadas:**
- Se escucha `EstaActualizandoCF` (no `CuentaFiscalActual`) para evitar una carrera con la recarga de licencia/usuarios.
- **Hallazgo clave (sigue vigente):** las pestañas viven como `Content` dentro de `MainTabbedPage`; su `OnAppearing` NO se dispara al volver de una página navegada. El hook correcto es `OnTabActivated()`.

---

## Pendiente acumulado

- **Tarjetas (backend + frontend):** Integración CouchDB completa. Falta probar flujo completo en dispositivo con backend levantado y verificar migración desde `SecureStorage`.
- **Widget iOS** equivalente al widget Android de captura (requiere Swift + WidgetKit).
- **Capturas — filtro "recién creado":** la ventana por `FechaCreacion` es heurística (±min); si el reloj del dispositivo difiere mucho del servidor podría fallar; vigilar.

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
