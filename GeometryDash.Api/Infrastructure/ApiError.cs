using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GeometryDash.Api.Infrastructure;

public sealed class ApiError(int status, string code, string message, Dictionary<string, string[]>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public Dictionary<string, string[]>? Errors { get; } = errors;
}
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var known = exception as ApiError;
        var unique = exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };
        var bad = exception is BadHttpRequestException;
        var status = known?.Status ?? (unique ? 409 : bad ? ((BadHttpRequestException)exception).StatusCode : 500);
        if (status == 500) logger.LogError(exception, "Error {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new {
            type = "about:blank", title = known?.Message ?? (unique ? "El recurso ya existe." : bad ? "Solicitud inválida." : "Error interno."),
            status, code = known?.Code ?? (unique ? "RESOURCE_CONFLICT" : bad ? "INVALID_REQUEST" : "INTERNAL_ERROR"),
            instance = context.Request.Path.Value, traceId = context.TraceIdentifier, errors = known?.Errors
        }, ct);
        return true;
    }
}
public static class RequestRules
{
    public static Guid UserId(this HttpContext context) => Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static void ExpectedVersion(HttpContext ctx, int actual)
    {
        var tag = ctx.Request.Headers.IfMatch.ToString();
        if (tag.Length == 0) throw new ApiError(428, "IF_MATCH_REQUIRED", "Envía If-Match con la versión del borrador.");
        if (tag != $"\"{actual}\"") throw new ApiError(412, "DRAFT_VERSION_CONFLICT", "El borrador cambió. Conserva tus cambios y recarga la versión remota.");
    }
    public static void Validate(Dictionary<string, string[]> errors)
    {
        if (errors.Count != 0) throw new ApiError(422, "LEVEL_GEOMETRY_INVALID", "Revisa los campos del nivel.", errors);
    }
    public static (int Page, int Size) Paging(int? page, int? pageSize)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100) throw new ApiError(400, "INVALID_PAGE", "Página o tamaño inválidos.");
        return (page ?? 1, pageSize ?? 20);
    }
}
