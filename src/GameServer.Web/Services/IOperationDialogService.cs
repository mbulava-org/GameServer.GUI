using GameServer.Web.Models;

namespace GameServer.Web.Services
{
    public interface IOperationDialogService
    {
        Task<OperationResult> ExecuteAsync(
            string title,
            Func<Task> operation,
            string? subtitle = null,
            string? errorTitle = null);

        Task<OperationResult> ExecuteAsync(
            string title,
            string? subtitle,
            Func<Task> operation,
            string? errorTitle = null);

        Task<OperationResult<T>> ExecuteAsync<T>(
            string title,
            Func<Task<T>> operation,
            string? subtitle = null,
            string? errorTitle = null);

        Task<OperationResult<T>> ExecuteAsync<T>(
            string title,
            string? subtitle,
            Func<Task<T>> operation,
            string? errorTitle = null);

        Task<OperationResult> ExecuteAsync(
            string title,
            Func<Task<OperationResult>> operation,
            string? subtitle = null,
            string? errorTitle = null);

        Task<OperationResult> ExecuteAsync(
            string title,
            string? subtitle,
            Func<Task<OperationResult>> operation,
            string? errorTitle = null);

        Task<OperationResult<T>> ExecuteAsync<T>(
            string title,
            Func<Task<OperationResult<T>>> operation,
            string? subtitle = null,
            string? errorTitle = null);

        Task<OperationResult<T>> ExecuteAsync<T>(
            string title,
            string? subtitle,
            Func<Task<OperationResult<T>>> operation,
            string? errorTitle = null);
    }
}
