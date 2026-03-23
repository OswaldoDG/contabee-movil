namespace ContaBeeMovil.Services
{
    /// <summary>
    /// Modal Error Handler.
    /// </summary>
    public class ModalErrorHandler : IErrorHandler
    {
        private readonly IServicioAlerta _servicioAlerta;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public ModalErrorHandler(IServicioAlerta servicioAlerta)
        {
            _servicioAlerta = servicioAlerta;
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
                await _servicioAlerta.MostrarAsync("Error", ex.Message, verBotonCancelar: false, confirmarText: "OK");
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}