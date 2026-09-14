using GameServer.Web.Components.Dialogs;
using GameServer.Web.Models;
using Radzen;

namespace GameServer.Web.Services
{
    public class OperationDialogService : IOperationDialogService
    {
        private readonly DialogService _dialogService;

        public OperationDialogService(DialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public async Task<OperationResult> ExecuteAsync(
            string title,
            Func<Task> operation,
            string? subtitle = null,
            string? errorTitle = null)
        {
            ArgumentNullException.ThrowIfNull(operation);

            return await ExecuteAsync(title, subtitle, async () =>
            {
                await operation();
                return OperationResult.Ok();
            }, errorTitle);
        }

        public async Task<OperationResult> ExecuteAsync(
            string title,
            string? subtitle,
            Func<Task> operation,
            string? errorTitle = null)
        {
            ArgumentNullException.ThrowIfNull(operation);

            return await ExecuteAsync(title, subtitle, async () =>
            {
                await operation();
                return OperationResult.Ok();
            }, errorTitle);
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(
            string title,
            Func<Task<T>> operation,
            string? subtitle = null,
            string? errorTitle = null)
        {
            ArgumentNullException.ThrowIfNull(operation);

            return await ExecuteAsync(title, subtitle, async () =>
            {
                var data = await operation();
                return OperationResult<T>.Ok(data);
            }, errorTitle);
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(
            string title,
            string? subtitle,
            Func<Task<T>> operation,
            string? errorTitle = null)
        {
            ArgumentNullException.ThrowIfNull(operation);

            return await ExecuteAsync(title, subtitle, async () =>
            {
                var data = await operation();
                return OperationResult<T>.Ok(data);
            }, errorTitle);
        }

        public Task<OperationResult> ExecuteAsync(
            string title,
            Func<Task<OperationResult>> operation,
            string? subtitle = null,
            string? errorTitle = null)
        {
            return ExecuteAsync(title, subtitle, operation, errorTitle);
        }

        public async Task<OperationResult> ExecuteAsync(
            string title,
            string? subtitle,
            Func<Task<OperationResult>> operation,
            string? errorTitle = null)
        {
            ArgumentNullException.ThrowIfNull(operation);

            OperationResult? finalResult = null;

            var dialogParams = new Dictionary<string, object?>
            {
                { nameof(OperationProgressDialog.Title), title },
                { nameof(OperationProgressDialog.Subtitle), subtitle },
                { nameof(OperationProgressDialog.ErrorTitle), errorTitle },
                {
                    nameof(OperationProgressDialog.Operation),
                    new Func<Task<OperationResult>>(async () =>
                    {
                        try
                        {
                            var res = await operation();
                            finalResult = res;
                            return res;
                        }
                        catch (Exception ex)
                        {
                            finalResult = OperationResult.Fail(ex.Message, ex);
                            return finalResult;
                        }
                    })
                }
            };

            var dialogOptions = new DialogOptions
            {
                ShowTitle = false,
                ShowClose = false,
                CloseDialogOnOverlayClick = false,
                CloseDialogOnEsc = false,
                Width = "450px",
                AutoFocusFirstElement = false
            };

            var result = await _dialogService.OpenAsync<OperationProgressDialog>(
                string.Empty,
                dialogParams,
                dialogOptions);

            if (result is OperationResult opResult)
            {
                return opResult;
            }

            return finalResult ?? OperationResult.Ok();
        }

        public Task<OperationResult<T>> ExecuteAsync<T>(
            string title,
            Func<Task<OperationResult<T>>> operation,
            string? subtitle = null,
            string? errorTitle = null)
        {
            return ExecuteAsync(title, subtitle, operation, errorTitle);
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(
            string title,
            string? subtitle,
            Func<Task<OperationResult<T>>> operation,
            string? errorTitle = null)
        {
            ArgumentNullException.ThrowIfNull(operation);

            OperationResult<T>? finalResult = null;

            var dialogParams = new Dictionary<string, object?>
            {
                { nameof(OperationProgressDialog.Title), title },
                { nameof(OperationProgressDialog.Subtitle), subtitle },
                { nameof(OperationProgressDialog.ErrorTitle), errorTitle },
                {
                    nameof(OperationProgressDialog.Operation),
                    new Func<Task<OperationResult>>(async () =>
                    {
                        try
                        {
                            var res = await operation();
                            finalResult = res;
                            return res;
                        }
                        catch (Exception ex)
                        {
                            finalResult = OperationResult<T>.Fail(ex.Message, ex);
                            return finalResult;
                        }
                    })
                }
            };

            var dialogOptions = new DialogOptions
            {
                ShowTitle = false,
                ShowClose = false,
                CloseDialogOnOverlayClick = false,
                CloseDialogOnEsc = false,
                Width = "450px",
                AutoFocusFirstElement = false
            };

            var result = await _dialogService.OpenAsync<OperationProgressDialog>(
                string.Empty,
                dialogParams,
                dialogOptions);

            if (result is OperationResult<T> opResult)
            {
                return opResult;
            }

            return finalResult ?? OperationResult<T>.Fail("Dialog closed without a result.");
        }
    }
}
