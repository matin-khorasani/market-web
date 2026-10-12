using Microsoft.EntityFrameworkCore;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);   // بقیه‌ی pipeline توی این خط اجرا می‌شه
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // کلاینت خودش درخواست رو قطع کرده (تب بسته، timeout). خطای سرور نیست.
            _logger.LogInformation("Request cancelled by client: {Path}", context.Request.Path);
        }
        catch (Exception ex)
        {
            // اگه جواب قبلاً شروع به ارسال شده، دیگه نمی‌تونیم status code رو عوض کنیم
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Exception after response started. TraceId {TraceId}", context.TraceIdentifier);
                throw;
            }

            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "The data was changed by someone else. Please retry."),
            _ => (StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.")
        };

        if (status >= 500)
            _logger.LogError(ex, "Unhandled exception. TraceId {TraceId}", context.TraceIdentifier);
        else
            _logger.LogWarning("Handled exception ({Type}): {Message}. TraceId {TraceId}",
                ex.GetType().Name, ex.Message, context.TraceIdentifier);

        // متن واقعی خطا فقط توی Development؛ توی Production هیچ جزئیات داخلی‌ای بیرون نمیاد
        var detail = _env.IsDevelopment() ? ex.Message : null;

        await Results.Problem(
                statusCode: status,
                title: title,
                detail: detail,
                extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
    }
}