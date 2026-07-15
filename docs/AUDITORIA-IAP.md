# Auditoría In-App Purchases — ContaBee

> **Fecha:** 2026-07-14
> **Alcance auditado (punta a punta):**
> - Cliente MAUI: `ContaBeeMovil/Services/IAP/ServicioIAP.cs`, `ContaBeeMovil/Pages/Tienda/TiendaPage.xaml.cs`, `Contabee.Api/ServicioEcommerce.cs`
> - Backend (`contabee-transcript-backend`): `InAppPurchaseController`, `ServicioInAppPurchase`, `ServicioValidacionCompra`, `ServicioCarritoCouchDb`, `ExtensionesCarrito`
> **Modelo de producto:** todos los productos son **consumibles** (créditos de un solo uso). No hay suscripciones → el único evento S2S relevante es **reembolso/revocación** (no renovaciones ni periodos de gracia).

---

## Tablero de avance

| # | Sev | Área | Hallazgo | Estado |
|---|-----|------|----------|--------|
| 1 | 🔴 Crítico | Backend | `/completar` NO revalida el recibo con la tienda → créditos gratis llamando directo al endpoint | [ ] Pendiente |
| 2 | 🔴 Crítico | Backend | Apple: se valida por `product_id` pero nunca por `transaction_id` → replay re-acredita | [ ] Pendiente |
| 3 | 🔴 Crítico | Backend | Idempotencia de Apple usa el recibo base64 completo como llave (no estable por transacción) | [ ] Pendiente |
| 4 | 🟡 Medio | Backend | `MontoCompra` lo pone el cliente y se persiste sin validar (integridad contable) | [ ] Pendiente |
| 5 | 🔴 Crítico | S2S | No hay procesamiento de reembolsos (Apple ASSN v2 / Google Voided Purchases) → reembolso = créditos gratis | [ ] Pendiente |
| 6 | 🟡 Medio | Backend | Apple usa `verifyReceipt` (deprecado por Apple) | [ ] Pendiente |
| 7 | 🔴 Crítico | Cliente | La reconciliación solo corre 1 vez, al abrir la Tienda → cobro sin acreditar si el usuario no vuelve | [ ] Pendiente |
| 8 | 🟡 Medio | Cliente | `ConsumirCompraAsync` traga errores → reprocesamiento y riesgo de doble crédito en Apple | [ ] Pendiente |
| 9 | 🟡 Medio | Cliente | `ComprarAsync == null` se trata como "cancelado" aunque sea fallo de red | [ ] Pendiente |
| 10 | 🟡 Medio | Cliente | Sin guard de reentrada en el botón comprar; overlay sin timeout ni cancelación | [ ] Pendiente |
| 11 | 🟢 Mejora | Cliente | Estados `Deferred`/`Purchasing` (Ask to Buy) se descartan sin encolar | [ ] Pendiente |
| 12 | 🟢 Mejora | Cliente | Recibo de Apple puede no existir en install limpio (falta refresh) | [ ] Pendiente |

### Orden de ataque sugerido para la semana
1. **Día 1 (dinero real, activo):** #1 y #5.
2. **Día 2–3:** #2 y #3 (van juntos; habilitan #5 y cierran el doble-crédito).
3. **Día 4:** #7 y #8 (evitan que usuarios legítimos pierdan créditos pagados).
4. **Backlog:** #4, #6, #9–#12.

---

# 1. Seguridad del backend

## 🔴 #1 — `/completar` acredita sin revalidar con la tienda
**Estado:** [ ] Pendiente

**El problema.** Dos endpoints desacoplados:
- `/verificar` → valida contra Apple/Google, **sin efecto** (no acredita).
- `/completar` → **sí acredita** (crea carrito, marca `Pagado`, dispara `AcreditaPago`), pero solo valida que los campos no estén vacíos:

```csharp
// ServicioInAppPurchase.CompletaCompraTienda
if (!ComprobanteEsValido(dtoComprobante)) { ... }   // solo checa strings no-vacíos
// ...nunca llama a ValidarTokenConTienda()
```

Nada obliga a que `/verificar` corra antes ni a que su resultado sea `Ok`. Cualquier usuario autenticado puede `POST /iappurchases/completar` con un body inventado y recibir créditos **sin pagar**.

**Solución.** Validación y acreditación **atómicas**: `completar` re-ejecuta `ValidarTokenConTienda` antes de tocar licenciamiento (o unificar en un solo endpoint verificar-y-completar).

```csharp
public async Task<Respuesta> CompletaCompraTienda(DtoComprobanteCompra dtoComprobante, Guid usuarioId, Guid? cuentafiscalId = null)
{
    var r = new Respuesta();

    if (!ComprobanteEsValido(dtoComprobante))
    {
        r.Ok = false; r.HttpCode = HttpStatusCode.BadRequest;
        r.Error = ErroresServicio.DatosNoValidos("...CompletaCompraTienda", "Comprobante invalido.");
        return r;
    }

    // ▼▼ NUEVO: revalida SIEMPRE contra la tienda antes de acreditar ▼▼
    var revalidacion = await ValidarTokenConTienda(dtoComprobante);
    if (!revalidacion.Ok)
    {
        logger.LogWarning("CompletaCompraTienda - token NO válido en tienda para {Producto}", dtoComprobante.ProductoTiendaId);
        r.Ok = false; r.HttpCode = revalidacion.HttpCode; r.Error = revalidacion.Error;
        return r;
    }
    // ▲▲ ahora sí, idempotencia + acreditación ▲▲

    var existeCarrito = await servicioCarrito.CarritoPorIdPasarela(dtoComprobante.PasarelaId);
    // ...resto igual
}
```

> Nota: para que `ValidarTokenConTienda` sea reutilizable aquí, conviene que devuelva la `Compra` validada (con el `transaction_id`), ver #2/#3.

---

## 🔴 #2 — Apple: se valida `product_id` pero nunca `transaction_id`
**Estado:** [ ] Pendiente

**El problema.** En `ValidarCompraApple`:

```csharp
if (response.receipt.in_app.Any(i => i.product_id == comprobante.ProductoTiendaId))
{
    r.Ok = true;  // basta con que EXISTA cualquier compra de ese producto
}
```

El recibo de Apple es **acumulativo**: contiene el historial. Para consumibles, una sola compra pasada de `captura500` deja ese `product_id` en el recibo para siempre → la validación devuelve `Ok` una y otra vez aunque no haya compra nueva. El `transaction_id` ya existe en el modelo `AppleInApp` pero **no se usa**.

**Solución.** Cotejar el `transaction_id` puntual (el cliente ya envía `CompraId = compra.TransactionIdentifier`) y devolverlo para usarlo como llave de idempotencia.

```csharp
// ValidarCompraApple — reemplaza el Any(product_id == ...) por:
var entrada = response.receipt.in_app
    .FirstOrDefault(i => i.product_id == comprobante.ProductoTiendaId
                      && i.transaction_id == comprobante.CompraId);

if (entrada is null)
{
    r.Ok = false; r.HttpCode = HttpStatusCode.BadRequest;
    r.Error = ErroresServicio.DatosNoValidos("...ValidarCompraApple",
        "No se encontró la transacción específica en el recibo.");
    r.Payload = new Compra { CompraId = comprobante.CompraId, Estado = EstadoCompra.Error };
    return r;
}

r.Ok = true;
// Devuelve el transaction_id validado como identificador canónico
r.Payload = new Compra { CompraId = entrada.transaction_id, Estado = EstadoCompra.Pagado };
```

> Ampliar `AppleInApp` con `original_transaction_id`, `quantity` y `cancellation_date` (este último detecta reembolsos ya presentes en el recibo, útil para #5).

---

## 🔴 #3 — Idempotencia de Apple usa el recibo base64 completo como llave
**Estado:** [ ] Pendiente

**El problema.**
```csharp
// CompletaCompraTienda → CarritoPorIdPasarela → WHERE PasarelaPagoTransaccionId == pasarelaId
```
- Google: `PasarelaId = PurchaseToken` → estable y único. **OK.**
- Apple: `PasarelaId = recibo base64 COMPLETO` → cambia al re-emitirse y crece con cada compra. Dos envíos de la misma compra con recibos distintos crean dos carritos → **doble acreditación**. Además es una columna gigante para indexar.

**Solución.** Llave de idempotencia = **`transaction_id`** (Apple) / **`purchaseToken`** (Google), no el blob.

```csharp
// En CompletaCompraTienda, tras revalidar (#1):
var revalidacion = await ValidarTokenConTienda(dtoComprobante);
if (!revalidacion.Ok) { ... }

var idTransaccion = revalidacion.Payload!.CompraId;   // Apple: transaction_id; Google: purchaseToken

var existeCarrito = await servicioCarrito.CarritoPorIdPasarela(idTransaccion);
if (existeCarrito.Ok && existeCarrito.Payload!.Estado == EstadoCarrito.Pagado)
{
    r.Ok = true;   // ← respuesta idempotente 200/OK (NO BadRequest), para que el cliente consuma tranquilo
    return r;
}
// ...guardar PasarelaPagoTransaccionId = idTransaccion (no el recibo completo)
```

> **Ojo cliente (#8):** hoy, cuando el carrito ya está pagado, el backend responde `BadRequest "Token ya procesado"`. Eso hace que el cliente crea que falló y **no consuma** la compra → queda atascada y se reintenta para siempre. Debe responder **200/OK idempotente**.

Guardar el recibo crudo en otra columna solo para auditoría, nunca como llave.

---

## 🟡 #4 — `MontoCompra` es client-side y se persiste sin validar
**Estado:** [ ] Pendiente

**El problema.** El cliente calcula `MontoCompra` y el backend lo guarda directo en `PrecioBase/SubTotal/Total/TotalImpuestos` (`CarritoInAppPusrhaseFromDto`). La validación de precio está comentada en `ProductosValidos`. Los créditos salen de metadata server-side (no es robo de créditos), pero el **monto contable es manipulable** (cliente alterado → `MontoCompra = 0`).

**Solución.** Ignorar el monto del cliente; tomar el precio del catálogo en el servidor.

```csharp
// CarritoInAppPusrhaseFromDto — en vez de carritoCompra.MontoCompra:
var precioServidor = producto.Precios.First(p => p.Tipo == TipoPrecio.Publico).Precio.Round2Decimals();
ElementoCompra elementoCompra = new()
{
    PrecioBase = precioServidor, SubTotal = precioServidor,
    Total = precioServidor, TotalImpuestos = /* impuestos server-side */,
    // ...
};
```

---

# 2. Ciclo de vida y notificaciones Server-to-Server

## 🔴 #5 — No hay procesamiento de reembolsos (ni S2S)
**Estado:** [ ] Pendiente

**El problema.** Cero manejo S2S en el pod de ecommerce. Sin Apple App Store Server Notifications V2 ni Google RTDN / Voided Purchases API. Flujo de fraude:
- Usuario compra 500 créditos → los recibe.
- Pide reembolso a Apple/Google (o chargeback) → le devuelven el dinero.
- El backend nunca se entera → **se queda con los créditos y con su dinero**.

Como los productos son **consumibles**, el reembolso es la vía de fraude principal.

**Solución.** Tres piezas nuevas:

**A) Apple — App Store Server Notifications V2** (URL registrada en App Store Connect; JWS firmado):
```csharp
[HttpPost("apple/notificaciones")]
[AllowAnonymous]  // Apple no manda tu JWT; la autenticidad la da la firma JWS
public async Task<IActionResult> AppleS2S([FromBody] AppleNotificationEnvelope envelope)
{
    var payload = await _appleJws.VerificarYDecodificar(envelope.SignedPayload);
    if (payload is null) return Unauthorized();

    switch (payload.NotificationType)
    {
        case "REFUND":
        case "REVOKE":
            var tx = await _appleJws.VerificarYDecodificar(payload.Data.SignedTransactionInfo);
            await _servicioReembolsos.RevocarPorTransaccion(PasarelaPago.Apple, tx.TransactionId);
            break;
    }
    return Ok();   // responde 200 rápido o Apple reintenta
}
```

**B) Google — Voided Purchases + RTDN (Pub/Sub)**:
```csharp
[HttpPost("google/notificaciones")]
[AllowAnonymous]
public async Task<IActionResult> GoogleRtdn([FromBody] PubSubEnvelope msg)
{
    var data = Decode(msg.Message.Data);   // base64 → DeveloperNotification
    if (data.VoidedPurchaseNotification is { } v)
        await _servicioReembolsos.RevocarPorTransaccion(PasarelaPago.Google, v.PurchaseToken);
    return Ok();
}
```

**C) Revocación común** (con el carrito ya indexado por transaction_id/purchaseToken tras #3):
```csharp
public async Task RevocarPorTransaccion(PasarelaPago pasarela, string idTransaccion)
{
    var carrito = await _repo.CarritoPorIdPasarela(idTransaccion);
    if (!carrito.Ok) { _logger.LogWarning("Reembolso sin carrito: {Id}", idTransaccion); return; }

    carrito.Payload.Estado = EstadoCarrito.Reembolsado;   // estado nuevo
    await _repo.Guardar(carrito.Payload);

    // Resta créditos ya acreditados (idempotente: piso en 0, marca para no doble-revocar)
    await _proxyLicenciamiento.RevocaCreditos(Guid.Parse(carrito.Payload.CuentaFiscalId!), acreditado);
}
```

Requiere: estado `EstadoCarrito.Reembolsado` + operación de licenciamiento que reste créditos (piso 0, anti doble-revocación).

> Renovaciones / gracia / cancelaciones **no aplican** (no hay suscripciones). Solo reembolso/revocación. Si algún día hay suscripciones, añadir ese ciclo completo.

---

## 🟡 #6 — Apple usa `verifyReceipt` (deprecado)
**Estado:** [ ] Pendiente

**El problema.** `ValidarCompraApple` usa `verifyReceipt`, deprecado por Apple. Recomendado: **App Store Server API** (validar por `transactionId` con JWS firmado, sin subir el recibo completo) o StoreKit 2.

**Solución (roadmap).** Migrar a `Get Transaction Info` (`/inApps/v1/transactions/{transactionId}`). El cliente manda solo `transactionId` (liviano) y el server pide el JWS. Resuelve #2 y #3 de raíz (trabajas por transacción, no por recibo acumulado). Ya tienes el retry 21007 (sandbox); falta 21008 (prod→sandbox) como caso simétrico.

---

# 3. Resiliencia del cliente móvil

## 🔴 #7 — La reconciliación solo ocurre al abrir la Tienda, una vez
**Estado:** [ ] Pendiente

**El problema.** Red de seguridad actual:
```csharp
// OnAppearing
if (_cargado) return;         // corre UNA sola vez por instancia de página
_cargado = true;
await CargarProductosAsync();
await ReintentarComprasPendientesLocalesAsync();   // cola local (Preferences)
await RestaurarComprasPendientesAsync();           // GetPurchasesAsync no-acknowledged
```
Huecos:
1. Solo corre al abrir `TiendaPage`. Si el cobro se hizo pero el usuario no vuelve a Tienda, el crédito no se acredita. En Android, tras ~3 días sin acknowledge, **Google reembolsa automáticamente** → el usuario perdió su compra.
2. Si la app muere **durante** `await PurchaseAsync` (cobro hecho, método nunca retornó), no se guarda nada local → solo `GetPurchasesAsync` puede rescatarla, y hoy depende de abrir la Tienda.

**Solución.** Reconciliación en **arranque de la app** y al volver de background, no solo en la Tienda.

```csharp
// App.xaml.cs
protected override void OnResume() => _ = _reconciliadorIAP.ReconciliarAsync();
// Startup, tras sesión lista:
await _reconciliadorIAP.ReconciliarAsync();
```
```csharp
public class ReconciliadorIAP
{
    public async Task ReconciliarAsync()
    {
        await ReintentarPendientesLocales();   // cola local persistida
        var pendientes = (await _iap.RestaurarComprasAsync())
            .Where(c => c.State == PurchaseState.Purchased && c.IsAcknowledged != true);
        foreach (var c in pendientes) await ProcesarConReintentos(c);
    }
}
```
Y que el guard aplique solo a la carga del catálogo:
```csharp
protected override async void OnAppearing()
{
    base.OnAppearing();
    // ...binding de controles...
    if (!_cargado) { _cargado = true; await CargarProductosAsync(); }
    await _reconciliadorIAP.ReconciliarAsync();   // SIEMPRE
}
```

---

## 🟡 #8 — `ConsumirCompraAsync` traga errores → reprocesamiento / doble crédito
**Estado:** [ ] Pendiente

**El problema.**
```csharp
catch
{
    // Si falla el consumo no bloqueamos al usuario — el backend ya acreditó
}
```
Si el consumo falla tras acreditar, la compra sigue "owned/unacknowledged" → la próxima reconciliación la re-envía al backend. Con Google + idempotencia por token no hay crédito extra (pero hoy el backend responde BadRequest, ver #3). En **Apple** con la idempotencia rota de #3 → **doble acreditación**. Además, un consumible no consumido en Android **no se puede recomprar**.

**Solución.** (a) Arreglar #3. (b) Consumo con resultado real y reintento persistente:
```csharp
public async Task<bool> ConsumirCompraAsync(string productId, string purchaseToken)
{
    var billing = CrossInAppBilling.Current;
    try
    {
        if (!await billing.ConnectAsync()) return false;
        return await billing.ConsumePurchaseAsync(productId, purchaseToken);
    }
    catch (Exception ex) { _logs.Error($"IAP consumo falló: {ex.Message}"); return false; }
    finally { await billing.DisconnectAsync(); }
}
```
Si devuelve `false`, dejar la compra en cola para reintentar el **consumo** (no solo la validación) — hasta consumir, Android puede reembolsar.

---

## 🟡 #9 — `PurchaseAsync == null` se trata como "cancelado"
**Estado:** [ ] Pendiente

**El problema.** `ComprarAsync` devuelve `null` tanto si falla `ConnectAsync` como si `PurchaseAsync` retorna null. El cliente muestra "Compra cancelada" para cualquier `null`, aunque haya sido fallo de red o deferred.

**Solución.** Tipo de retorno rico:
```csharp
public enum ResultadoCompra { Ok, Cancelada, SinConexion, Pendiente, Error }
public record CompraResult(ResultadoCompra Estado, InAppBillingPurchase? Compra, string? Detalle);

public async Task<CompraResult> ComprarAsync(string productId)
{
    var billing = CrossInAppBilling.Current;
    try
    {
        if (!await billing.ConnectAsync())
            return new(ResultadoCompra.SinConexion, null, "No se pudo conectar a la tienda");

        var compra = await billing.PurchaseAsync(productId, ItemType.InAppPurchase);
        if (compra is null) return new(ResultadoCompra.Cancelada, null, null);
        if (compra.State is PurchaseState.Deferred or PurchaseState.Purchasing)
            return new(ResultadoCompra.Pendiente, compra, null);
        return new(ResultadoCompra.Ok, compra, null);
    }
    catch (InAppBillingPurchaseException ex) when (ex.PurchaseError == PurchaseError.UserCancelled)
    {
        return new(ResultadoCompra.Cancelada, null, null);
    }
    catch (Exception ex) { return new(ResultadoCompra.Error, null, ex.Message); }
    finally { await billing.DisconnectAsync(); }
}
```
"Compra cancelada" solo con `PurchaseError.UserCancelled`; `SinConexion`/`Pendiente` disparan flujos distintos.

---

## 🟡 #10 — Sin guard de reentrada; overlay sin timeout
**Estado:** [ ] Pendiente

**El problema.** `OnComprarClicked` es `async void` sin bandera de "en curso" → doble-tap dispara dos `PurchaseAsync`. `SetCargando(true)` bloquea la UI durante la validación; si el backend cuelga, el usuario queda atrapado (sin timeout visible en las llamadas HTTP).

**Solución.**
```csharp
private bool _comprando;

private async void OnComprarClicked(object sender, EventArgs e)
{
    if (_comprando) return;
    _comprando = true;
    if (sender is Button b) b.IsEnabled = false;
    try { /* flujo con timeout en las llamadas al backend */ }
    finally
    {
        _comprando = false;
        if (sender is Button b2) b2.IsEnabled = true;
        SetCargando(false);
    }
}
```
En `ServicioEcommerce`, aplicar `CancellationToken` con timeout (20–30 s) a `VerificarAsync`/`CompletarAsync`. Si expira, la compra cae a la cola local y se reintenta — sin dejar la UI colgada.

---

## 🟢 #11 — Estados `Deferred`/`Purchasing` se descartan
**Estado:** [ ] Pendiente

En `ProcesarCompraAsync`, `Purchasing`/`Deferred` (Ask to Buy, SCA) solo loguean y devuelven `false`, sin encolar. Cuando Apple/Google confirmen después, nada los rescata salvo `GetPurchasesAsync` en la próxima apertura de Tienda. Con el reconciliador de #7 queda cubierto (reconciliar al arrancar). No requiere persistir el `Deferred` (aún no hay token).

---

## 🟢 #12 — Recibo de Apple ausente en install limpio
**Estado:** [ ] Pendiente

En `EnviarAlBackendYCompletarAsync`, si `AppStoreReceiptUrl` no existe (install limpia / primer arranque), se hace `verificationData ??= compra.OriginalJson`, que **no es un recibo válido** para `verifyReceipt` → la validación fallará. Forzar refresh (`SKReceiptRefreshRequest`) antes de rendirse, o —mejor— migrar a mandar solo `transactionId` (App Store Server API, #6), que no depende del archivo de recibo.

---

## Notas de seguimiento
- Marca cada punto cambiando `[ ] Pendiente` → `[x] Hecho` (y en la tabla superior).
- #2 y #3 se implementan juntos (dependen del mismo cambio en `ValidarTokenConTienda`).
- #1 desbloquea la reutilización de `ValidarTokenConTienda` que necesitan #2/#3/#5.
