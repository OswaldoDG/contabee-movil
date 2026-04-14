using ContaBeeMovil.Services.Dev;

namespace ContaBeeMovil.Services
{
    /// <summary>
    /// Modal Error Handler.
    /// </summary>
    public class ModalErrorHandler : IErrorHandler
    {
        private readonly IServicioAlerta _servicioAlerta;
        private readonly IServicioLogs _logs;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public ModalErrorHandler(IServicioAlerta servicioAlerta, IServicioLogs logs)
        {
            _servicioAlerta = servicioAlerta;
            _logs = logs;
        }

        /// <summary>
        /// Handle error in UI.
        /// </summary>
        /// <param name="ex">Exception.</param>
        public void HandleError(Exception ex)
        {
            DisplayAlertAsync(ex).FireAndForgetSafeAsync();
        }

        async Task DisplayAlertAsync(Exception ex)
        {
            try
            {
                await _semaphore.WaitAsync();
                _logs.Log($"[ModalErrorHandler] {ex.GetType().Name}: {ex.Message}");
                await _servicioAlerta.MostrarAsync("Error", "Ocurrió un error inesperado.", verBotonCancelar: false, confirmarText: "OK");
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}