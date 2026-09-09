# ContaBee Movil — Contexto acumulado

> Este archivo se actualiza al final de cada conversación relevante.  
> Formato: sección más reciente arriba. Incluir fecha, qué se hizo, qué quedó pendiente.

---

## 2026-09-09 — Primer build Android en GitHub Actions

**Resultado inicial:**
- El workflow reconstruyó correctamente el keystore y confirmó la misma huella SHA-256 registrada en Google Play.
- Falló en `dotnet restore --locked-mode` con `NU1004`: el restore Android de Linux se estaba comparando contra el lockfile MAUI multiplataforma.

**Corrección:**
- Agregada la propiedad de build `ContaBeeAndroidOnly` para que el script limite únicamente el proyecto MAUI a `net10.0-android`, sin alterar el framework `net10.0` de `Contabee.Api`.
- Agregado `ContaBeeMovil/packages.android.lock.json`, separado del lockfile multiplataforma que se conservará para iOS.
- `build-android-release.sh` usa esa propiedad durante restore y publish; el publish lleva `--no-restore` para no ejecutar una segunda restauración con otra configuración.

**Verificación:**
- El mismo comando de restore bloqueado que usará CI terminó correctamente para `ContaBeeMovil` y `Contabee.Api`.
- Pendiente volver a ejecutar `Build Android` sobre la rama con esta corrección.

---

## 2026-09-08 — Preparación para automatizar builds y publicación desde GitHub

**Etapa 0: completada.**

**Hecho:**
- Seguridad: se quitó de la URL del remoto local el PAT de GitHub que estaba embebido en texto claro. El usuario confirmó que el token fue revocado; el remoto quedó como URL HTTPS sin credenciales.
- Versionado iOS: `ContaBeeShareExtension/Info.plist` ya no fija `1.0.32 (32)`. La extensión hereda `ApplicationDisplayVersion`/`ApplicationVersion` de la app mediante `AdditionalProperties`. Verificado con build Release de simulador: app y extensión generan `2.5.10 (65)`.
- Firma iOS: `build-release.sh` dejó de re-firmar manualmente el contenido del IPA (el `ContaBeeMovil_signed.ipa` existente fallaba `codesign --verify --deep --strict`). Ahora usa el IPA producido y firmado por `dotnet publish`, limpia también `bin/obj` de la Share Extension para no reutilizar su `Info.plist`, valida firma profunda de app/extensión y comprueba que ambas versiones coincidan.
- Reproducibilidad: agregado `global.json` para el SDK `10.0.201` con `latestPatch`; `Plugin.Maui.OCR` quedó fijado en `1.1.1`; generados `packages.lock.json` para los tres proyectos.
- Perfiles auditados: existen perfiles App Store de app y Share Extension, ambos con vencimiento 1-abr-2027.
- Android: localizado el keystore vigente fuera del repositorio (`../contabee-release.keystore`), alias `contabee`; nunca apareció en el historial y `*.keystore`/`*.jks` ya están ignorados. Permisos restringidos de `644` a `600`. Agregado `build-android-release.sh`: lee contraseña de forma interactiva o por variables CI, usa archivos temporales para MSBuild, fuerza formato AAB y valida firma con `jarsigner`.
- Corrección posterior: todas las rutas de `build-android-release.sh` se convierten en absolutas. MSBuild resolvía la ruta relativa del keystore desde el directorio del `.csproj` y producía `XA4310` aunque el archivo sí existía.
- Google Play: el usuario confirmó que la huella SHA-256 del certificado usado para firmar el AAB coincide con el `Upload key certificate` configurado en Play Console.

**Verificación:**
- `dotnet build ContaBeeMovil/ContaBeeMovil.csproj -f net10.0-ios -r iossimulator-arm64 -c Release -p:ArchiveOnBuild=false -p:EnableCodeSigning=false --no-restore`: exitoso, 0 errores (warnings preexistentes).
- `./build-release.sh` fuera del sandbox: exitoso. Generó `ContaBeeMovil.ipa`; firma profunda válida para app y Share Extension; ambas quedaron en `2.5.10 (65)`.
- `./build-android-release.sh`: exitoso. Generó `mx.contabee.app-Signed.aab` (54 MB), validado por `jarsigner` (`jar verified`). Certificado autofirmado RSA 2048/SHA384, vigente hasta 17-ago-2053; SHA-256 de la clave de carga: `BE:07:DF:3F:11:04:E1:9A:3F:E0:43:64:1C:8C:5D:40:EB:00:A3:4D:E6:C2:6C:6F:98:D4:D7:36:16:22:8E:10`.
- `dotnet restore ContaBeeMovil.slnx --locked-mode`: exitoso para los tres proyectos.
- `bash -n build-release.sh`, `plutil -lint ContaBeeShareExtension/Info.plist` y `git diff --check`: exitosos.

**Siguiente etapa:**
- Etapa 1 iniciada: creado `.github/workflows/build-android.yml`. Se ejecuta manualmente, instala el SDK fijado por `global.json` y el workload `maui-android`, reconstruye el keystore desde secretos, llama a `build-android-release.sh` y conserva el AAB firmado como artifact durante 14 días.
- El usuario confirmó que `ANDROID_KEYSTORE_BASE64` y `ANDROID_SIGNING_PASSWORD` ya están registrados como GitHub Actions Secrets.
- Seguridad preventiva para la siguiente parte: agregados `*.p12` y `*.mobileprovision` a `.gitignore`; no hay archivos de firma rastreados por Git.
- Pendiente: el usuario hará el commit, el merge a `main` y el push; después se debe probar la primera ejecución de Android desde Actions.
- Después de validar Android en GitHub, crear el workflow equivalente de iOS.

---

## 2026-08-17 — Aviso de horario con fotos: coach mark de la mascota (estilo tutorial)

**Idea de Beto:** con fotos ya tomadas, el aviso de fuera de horario lo da la mascota entrando desde la esquina izquierda con un globo de diálogo, cerca del botón Enviar — como el tutorial de un videojuego.

**Hecho** — nuevo control `Views/MascotaAvisoView.xaml(.cs)`, reusable:
- API: `Mensaje`, `Titulo` (bindables), `MostrarAsync(segundos = 6)`, `OcultarAsync()`, `OcultarInmediato()`.
- **Primera versión se descartó por parecer una notificación** (mascota como ícono dentro de una tarjeta + globo con pico, todo entrando junto). La diferencia con un cuadro de diálogo de juego **no está en la caja sino en el personaje**; la versión vigente:
  1. **La mascota va PARADA ENCIMA del borde superior de la caja**, medio cuerpo fuera, dibujada después de la caja en el XAML para quedar por delante del marco. Ya no hay globo ni pico: la caja *es* el diálogo, y así el texto usa el ancho completo.
  2. **Entrada en dos vectores a la vez**: la caja se desliza desde el borde izquierdo (`CubicOut`) y 130 ms después la mascota se deja caer desde arriba con `BounceOut` + `SpringOut` en escala y giro (aterriza, rebota y se endereza). Que todo entre junto es justo lo que la hacía leerse como notificación.
  3. **Bamboleo en bucle** (±6 px, 1.7 s) mientras está en pantalla: es lo que hace que el personaje se sienta vivo y no una imagen pegada.
  4. **Tecleo letra por letra** del mensaje, y al terminar parpadea el chevron de "continuar". **Tap a media frase completa el texto de golpe; tap con el texto completo cierra** — comportamiento de cuadro de diálogo de juego.
  - Marco de **2 px en `Primary`** en vez del filo hairline de las tarjetas de la app: el borde grueso de color es lo que lo lee como UI de juego.
- **`LblMedida`**: copia del mensaje completo en `Opacity=0` detrás del visible. Reserva el alto de todas las líneas desde el primer frame; sin él el tecleo iría agregando renglones y la caja daría saltos de altura a media frase. Las dos etiquetas deben mantener idénticos fuente y tamaño.
- Todo se anima con `Animation` comprometidas contra la vista (`Commit`/`AbortAnimation`) y **no** con bucles de `Task.Delay`: van sincronizadas al ticker de la plataforma y se cortan de golpe, que es lo que necesitan el bamboleo y el parpadeo (bucles infinitos). El `finished` de una `Animation` corre **también al abortarla**, y de eso depende que el `await` del tecleo siempre libere.
- El desplazamiento de entrada usa el **ancho del display**, no el de la caja: con `IsVisible=false` la vista no está medida (`Width = 0`) y quedarse corto hace que la caja "aparezca" a medio camino.
- **Halo detrás de la mascota** (`Light=#26FEC001 / Dark=#3DFFFFFF`): no es decoración — `contabeepet.png` es un dibujo de contornos negros y suelto sobre el fondo oscuro (#141414) perdería la silueta.
- ⚠ **NO lleva metadata `Inflator` en el csproj** — ver abajo, el bug de arranque que destapó.

**El coach mark quedó como ÚNICO aviso de horario.** Los otros tres se desactivaron con `IsVisible="False"` + comentario `[DESACTIVADO]` (mismo patrón que el badge del botón Enviar), sin borrar el markup:
1. **Aviso amplio** (sin fotos, mascota en grande al centro) — con él encendido el mismo mensaje se daba dos veces seguidas: mascota grande al entrar, y otra vez la del coach mark al tomar la primera foto. Al apagarlo hubo que devolver `MostrarEstadoVacio` a `!TieneCapturas`, o la zona de capturas quedaba en blanco fuera de horario.
2. **Franja de estado** (`Se procesará el lunes 9:00 a.m.`). ⚠ **Al apagarla se perdió la única forma de volver a leer el aviso**: el coach mark sale una vez por lote y se retrae solo, y era el tap en la franja lo que lo volvía a llamar. Es lo primero que hay que reactivar si eso hace falta — `OnAvisoHorarioTapped` sigue existiendo y sigue apuntando al coach mark, así que es cambiar una línea.
3. **Bloque dentro del selector "Quién captura"**. Era el más defendible de los tres: es el único momento en que el dato es **accionable**, porque ahí el usuario todavía puede mandar el lote a Mi Equipo, que no depende del horario de Contabee. Si alguno merece volver, es éste.

**En `PaginaCaptura`:**
- El coach mark se monta en el Grid raíz con `Margin="10,0,10,56"` (apoyado sobre la fila de botones sin comerse su área de toque), **después** del contenido y **antes** del scrim, para que el flyout "Quién captura" lo tape.
- Disparo enganchado a `NotificarPanelCentral()` — ahí desembocan todos los caminos que agregan fotos (cámara, imagen compartida y fotos guardadas pasan por `OnCapturasCollectionChanged`), más el cambio de crédito y el timer del minuto. La bandera `_coachMarkHorarioMostrado` es la que garantiza **una vez por lote**; se rearma al quedar el lote en 0 (envío exitoso o borrar todas).
- Delay de 450 ms antes de animar: con fotos guardadas el disparo cae dentro de `OnAppearing` y sin la pausa la entrada se ve a tirones. `OnDisappearing` llama `OcultarInmediato()` para no dejar animaciones corriendo sobre una página que ya se fue.
- Tocar la **franja de estado** ahora vuelve a llamar a la mascota en vez de abrir el popup centrado.

**Redacción nueva (dada por negocio):** título **"Sigue enviando tus fotos"** + mensaje *"Estamos fuera de horario hábil pero las procesaremos a la brevedad {hoy | mañana | el próximo lunes} a primera hora, o antes si es posible."*
- El título se aplicó a **las tres vistas** que muestran `Mensaje` (aviso amplio, coach mark y tarjeta del selector "Quién captura") porque el **"las" de "las procesaremos" toma de ahí su antecedente**. Sustituye al viejo "Fuera de horario" del selector y al "Haremos lo posible por tenerlas antes…" del aviso amplio, que ahora sería redundante. Si se cambia el título, revisar que el pronombre siga teniendo a qué referirse.
- **La regla se enunció como "menos de 24 h → mañana; más de 24 h → día de la semana", pero se implementó por día de calendario.** Coinciden en todos los casos menos uno, y ahí la de 24 h se equivoca: a las **7:00 de un martes** la reanudación es ese mismo martes a las 9:00 — faltan 2 horas ("menos de 24") pero decir "mañana" sería falso. Por eso existe la rama **"hoy"**, que la regla original no contempla porque sólo pensaba en el caso de después de las 18:00.
- Se agregó coma antes de "o antes si es posible" y punto final; el resto es literal.
- `MensajeBreve` (la franja: *"Se procesará el lunes 9:00 a.m."*) y `ResumenCorto` (*"reanuda lun 9:00"*) **no se tocaron**: son huecos de una línea y ahí sí se espera la hora exacta. La redacción nueva dice "a primera hora" a propósito — el compromiso es de prontitud, no de reloj.

**Decisiones tomadas (con Beto):**
- **No bloquea, sin velo.** Se descartó el modal con scrim de la imagen de referencia: interrumpe justo cuando el usuario iba a enviar y contradice la decisión vigente de que el horario sólo informa.
- **Se retrae sola a los 6 s** contados **desde que termina de teclear** (no desde que aparece), dejando la franja de estado como rastro permanente — tocarla la vuelve a llamar. Se descartó que se quedara hasta cerrarla porque tapa la esquina del carrusel mientras el usuario decide.
- **Una vez por lote**, no por foto: a la tercera foto la animación estorbaría a quien captura varios tickets seguidos.

**Bug de arranque destapado de paso: `Inflator` en el csproj tronaba la app, y la nota de julio tenía el diagnóstico al revés.**
- Síntoma: `XamlParseException: No embeddedresource found for __XamlGeneratedCode__.__Type21484A13DD1B76E5` al correr. El tipo NO era ninguno de los archivos nuevos: era **`Resources/Styles/AppStyles.xaml`**, que `App.xaml` carga al arrancar.
- Causa real: con `MauiXamlInflator=SourceGen` **el .xaml no se embebe como recurso** (verificado sobre el binario: 0 ocurrencias de XML de XAML dentro de `ContaBeeMovil.dll`; los `ContaBee.*.xaml` que sí aparecen son argumentos del atributo `XamlResourceId`, no recursos). SourceGen emite para cada XAML un stub `.sg.cs` cuyo `InitializeComponent` llama `LoadFromXaml`, y aparte el `.xsg.cs` con la implementación real que lo reemplaza. **Poner `Inflator` impide que se genere el `.xsg.cs`**, así que el archivo se queda con el stub, llama `LoadFromXaml` y busca el recurso inexistente. `Inflator="Runtime"` falla siempre; `Inflator="Default"` falla en Debug (Default = Runtime en Debug, XamlC en Release) — por eso `ActualizacionPopup` "pasó" las pruebas de julio, que fueron en Release.
- Se eliminaron **los dos** overrides del csproj (`AppStyles.xaml` y `ActualizacionPopup.xaml`) y quedó un comentario grande prohibiéndolos. Comprobado con `-p:EmitCompilerGeneratedFiles=true`: antes 74 `.xsg.cs` y esos dos archivos sin el suyo; después 76, y **ningún** XAML del proyecto se queda sin implementación compilada.
- **Costo:** se pierde XAML Hot Reload sobre esos dos archivos. Es lo que se estaba comprando a cambio de que la app no arrancara.
- Cómo diagnosticar si vuelve a pasar: `dotnet build -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=<dir>` y buscar el hash del error en `<dir>` — el archivo que lo declara es el culpable.

**Se evaluó una segunda opción y se descartó.** `MascotaAvisoComicView` (mascota a la **derecha**, globo con pico y título en mayúsculas, sin tarjeta detrás) convivió con la actual detrás de un conmutador de Modo Dev. **Beto eligió la de la izquierda**; el control, la interfaz `IAvisoMascota` y el conmutador se eliminaron. Recuperable del historial de git si se quiere retomar.

**Ajustes finales pedidos por Beto sobre la opción ganadora:**
- Se probó **la caja arriba y la mascota abajo** para que el texto no se disputara la franja inferior con el selector "Quién captura", y **se revirtió**: quedó la disposición original —mascota parada sobre el borde superior de la caja— y el conflicto con el selector se resolvió por el otro lado (ver abajo: el aviso sube por encima de la tarjeta).
- **Efecto de capas**: la mascota se lee como un plano por delante de la caja. Lo que lo consigue es que su **disco de fondo pasó de velo translúcido a opaco (`CardBackground`) con filo hairline y sombra propia** — sin algo opaco que proyecte sombra, mascota y caja se ven como un solo plano. Y el solape subió de 28 a 40 px (`Margin` superior de la caja 72 → 60).
  - ⚠ La sombra va en ese `Border` y **no en el `Image`**: en Android la sombra de una imagen con transparencia sigue el rectángulo del control, no la silueta del dibujo — saldría un cuadro oscuro detrás de la mascota.
  - El disco opaco además sustituye al halo como solución del contraste en tema oscuro (los contornos negros de `contabeepet.png` sobre #141414).
- **Botón de cerrar (✕) en su propia fila, abajo a la derecha**, no junto al título. Ahí arriba compite con el hueco de 96 px de la mascota y con el reloj, y en pantallas de 360 dp no quedaba ancho para que el título entrara en una sola línea.
- **Se quitó el tecleo letra por letra.** El mensaje aparece completo. Eso volvió innecesario el truco de `LblMedida` (la copia en opacidad 0 que reservaba el alto para que la caja no saltara de tamaño a media frase) y también el chevron de "continuar" con su parpadeo: se eliminaron los tres.
- **Ya no se retrae solo** — se queda hasta que lo cierren. Se fue toda la maquinaria de auto-ocultado (`CancellationTokenSource`, `segundosVisible`). Tocar la caja también cierra, como antes.
- Lo único que quedó del "está vivo" es el **bamboleo en bucle**, que ahora corre indefinidamente mientras el aviso esté en pantalla.

**Se está probando una tercera forma: `MascotaVinetaView` (la activa hoy).** Tira de cómic **vertical pegada al borde izquierdo**, globos de diálogo arriba y la mascota al pie; entra deslizándose desde la izquierda y los globos brotan **uno tras otro** (simultáneos parecerían dos bloques de texto sueltos). `MascotaAvisoView` se conserva sin borrar: **las dos exponen la misma API**, así que volver es cambiar el tipo en el XAML de `PaginaCaptura`, sin tocar el code-behind.
- **Media pantalla exacta, sin cuentas:** la raíz es un `Grid` de dos columnas iguales del que sólo se usa la primera. Nada de `WidthRequest` calculado.
- **Sin marco ni fondo**: el contenedor es transparente, sólo se ven la mascota (grande, a todo el ancho de la media pantalla) y los globos. Nació con marco grueso de viñeta y **Beto pidió quitarlo**. Dos consecuencias: (1) el contraste pasó a cada globo, que llevan **sombra propia** — en tema claro el blanco del globo y el #fefdfc del fondo son casi el mismo color; (2) la mascota se quedó **sin respaldo**, así que en tema oscuro sus contornos negros pierden nitidez contra el #141414. Es el precio aceptado de quitar la card.
- Sin fondo, el contenedor ya no captura toques de forma fiable: **el tap para cerrar vive en cada globo**, que sí tienen relleno.
- **El margen superior lo pone la página** (`ActualizarMargenAviso`), no el XAML: sale de `ZonaCapturas.Y` — donde terminan los formularios, que cambia al desplegar "Más opciones". Se llama en `OnZonaCapturasSizeChanged` **fuera del guard de "el alto no cambió"**, porque la posición de la zona de fotos se mueve aunque el alto acabe igual.
- **Al abrir el selector, es el SELECTOR el que se repliega a la mitad derecha** (`ActualizarMargenFlyout`), no la tira la que se encoge — ocupa justo el hueco que ella deja. Sin la tira en pantalla recupera su ancho completo. ⚠ A media pantalla las dos tarjetas de crédito ("Mi Equipo" / "Contabee") quedan estrechas; si el texto se corta, toca apilarlas en vertical.
- Globos sin trazo y picos `Polygon` del mismo relleno: con borde propio habría que tapar la línea del globo en la base del pico y esa costura siempre se ve.

**Dos consecuencias de que el aviso ya no se cierre solo, y cómo se resolvieron:**
- **Abrir una foto lo mataba para siempre.** El visor de imagen navega → `OnDisappearing` → `OcultarInmediato()`, y al volver la bandera de "una vez por lote" impedía que el disparo normal lo repitiera. Ahora `OnDisappearing` guarda `_avisoHorarioPendienteReanudar = AvisoMascotaHorario.IsVisible` y `OnAppearing` lo vuelve a sacar. La distinción clave: **si el usuario lo cerró, `IsVisible` ya era false**, así que cerrar sigue siendo definitivo. Al reanudar se revalida `MostrarAvisoHorario && TieneCapturas` por si entró el horario hábil mientras estuvo fuera.
- **Se disputaba la franja inferior con el selector "Quién captura".** En vez de ocultarlo —el usuario necesita leerlo justo cuando decide quién captura— **el aviso sube por encima de la tarjeta al abrirla y baja al cerrarla** (`SubirAvisoSobreFlyoutAsync`). Además se movió en el XAML a **después** del selector, para que el scrim no lo atenúe y siga legible.
  - El desplazamiento se calcula con `FlyoutCredito.Height` + los márgenes, que están **replicados como constantes en el code-behind** (`MargenInferiorFlyout=74`, `MargenInferiorAviso=56`): si cambian en el XAML hay que actualizarlos ahí.
  - Lleva un `Task.Delay(16)` antes de medir: en la **primera** apertura la tarjeta aún no está medida y `Height` devuelve 0, lo que dejaría el aviso encimado en vez de arriba. Hay un valor de respaldo (170) por si aun así llega en 0.

**Pendiente:**
- Probar en dispositivo (Android/iOS). Compiló verde en `net10.0-android` (`-t:Compile`, 0 errores).
- **Sin verificar:** en un build incremental aparecieron 4 XAML preexistentes sin `.xsg.cs` (`DetalleComprobacionPage`, `PaginaComprobaciones`, `DetalleDevolucionPage`, `RestablecerContrasenaPage`). El compile completo anterior daba 0, así que lo más probable es que sea artefacto de `EmitCompilerGeneratedFiles` en incremental — y esas páginas funcionan en producción, cosa imposible si de verdad cayeran a `LoadFromXaml`. **No se pudo confirmar con un `-t:Rebuild`: el `obj\` estaba bloqueado por el IDE.** Vale la pena rehacer la medición tras un Rebuild limpio.
- El csproj tiene **7 entradas `MauiXaml Update` duplicadas** (`FiltrosDevolucionesView`, `CrearDevolucionPopup`, `ActualizarDevolucionPopup`, `ActualizarComprobacionPopup`, `DetalleDevolucionPage`, `PaginaComprobaciones`, `DetalleComprobacionPage`). No parecen causar daño —todas tienen su `.xsg.cs`— pero son ruido y conviene limpiarlas.
- **`Views/HorarioCapturaPopup.xaml` quedó sin usar** — era el único destino del tap en la franja. Se conserva por si el coach mark no convence en dispositivo; si convence, se puede borrar.

---

## 2026-08-06 — Permiso de cámara: re-pedir en cada intento + salida a Ajustes

**Bug:** si el usuario negaba el permiso de cámara (aunque fuera por error), la función quedaba muerta: `TomarFotoAsync`/`EscanearQrAsync` llamaban `Permissions.RequestAsync` y con `!= Granted` devolvían `string.Empty` **sin decir nada**. Tras la 2ª negación (Android) o la 1ª (iOS) el SO ya no muestra su diálogo, así que el botón dejaba de responder para siempre.

**Hecho** — nuevo `Services/Permisos/IServicioPermisos` + `ServicioPermisos` (singleton en `MauiProgram`), único punto para pedir permisos:
- `CheckStatusAsync` → si ya está concedido, ni molesta. Si no, **re-pide en cada intento** (la negación no se cachea nunca).
- Distingue negación temporal de permanente con **`Permissions.ShouldShowRationale<T>()`**: `true` → el SO sí volverá a preguntar, se ofrece "Permitir" y se re-pide en un loop; `false` → negación permanente (Android "no volver a preguntar" / iOS cualquier negación) → alerta con **"Abrir ajustes"** (`AppInfo.Current.ShowSettingsUI()`), que es el único camino real.
- Todo lo de `Permissions` va por `MainThread.InvokeOnMainThreadAsync` con cast explícito (`(Func<Task<PermissionStatus>>)`) — sin el cast la sobrecarga `Func<T>` vs `Func<Task<T>>` queda ambigua y no compila.
- `ServicioCamara` (foto y QR) y `QRPage.OnAppearing` ya solo llaman al servicio. `QRPage` **cierra el modal** si no hay permiso (antes dejaba visor negro con un alert sin salida) y al volver de Ajustes su `OnAppearing` re-evalúa → el escáner queda operativo sin reiniciar la app.

**Pendiente:** probar en dispositivo el ciclo completo (negar 1 vez → volver a tocar → aparece diálogo; negar 2 veces → "Abrir ajustes" → activar → tocar de nuevo funciona), en Android e iOS. Compiló verde (`-t:Compile`, net10.0-android). El permiso de **notificaciones iOS** (`SharedImageHandler`) sigue pidiéndose solo si `NotDetermined` y no ofrece Ajustes — quedó fuera de alcance.

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

**Trampa de `MauiXamlInflator=SourceGen` (csproj:28) — leer antes de agregar XAML nuevo.** Al correr el popup por primera vez reventó con `XamlParseException: No embeddedresource found for __XamlGeneratedCode__.__Type<hash>`. Se aplicó `<MauiXaml Update="Views\ActualizacionPopup.xaml" Inflator="Default" />` copiando el precedente de `AppStyles.xaml` (`Inflator="Runtime"`).

> ⛔ **CORRECCIÓN (2026-08-17): ese diagnóstico estaba al revés y el "arreglo" era la CAUSA.** Ver la entrada del 2026-08-17; ambos overrides se eliminaron del csproj. El síntoma que se anotó aquí como evidencia a favor —"con `Inflator="Default"` deja de generarse el `.xsg.cs` del archivo"— es exactamente el defecto: sin `.xsg.cs` el archivo se queda sólo con el stub `.sg.cs`, que llama `LoadFromXaml` y busca un recurso que bajo SourceGen no existe.

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
