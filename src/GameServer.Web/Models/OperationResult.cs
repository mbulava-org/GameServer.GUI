namespace GameServer.Web.Models
{
    public class OperationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }

        public static OperationResult Ok() => new() { Success = true };
        public static OperationResult Fail(string errorMessage, Exception? exception = null) =>
            new() { Success = false, ErrorMessage = errorMessage, Exception = exception };
    }

    public class OperationResult<T> : OperationResult
    {
        public T? Data { get; set; }

        public static OperationResult<T> Ok(T data) => new() { Success = true, Data = data };
        public static new OperationResult<T> Fail(string errorMessage, Exception? exception = null) =>
            new() { Success = false, ErrorMessage = errorMessage, Exception = exception };

        public static implicit operator OperationResult<T>(T data) => Ok(data);
    }
}
