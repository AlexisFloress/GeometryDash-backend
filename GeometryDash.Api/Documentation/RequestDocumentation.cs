using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GeometryDash.Api.Documentation;

public sealed class RequestDocumentation : IOpenApiOperationTransformer
{
    private static readonly JsonObject Requests = Load();

    private static JsonObject Load()
    {
        using var stream = typeof(RequestDocumentation).Assembly.GetManifestResourceStream("GeometryDash.Api.Documentation.requests.json")
            ?? throw new InvalidOperationException("Falta la documentación de solicitudes.");

        return JsonNode.Parse(stream)!.AsObject();
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var path = "/" + context.Description.RelativePath?.Split('?')[0].Replace(":guid", "");
        var method = context.Description.HttpMethod;
        var source = Requests[$"{method} {path}"]?.AsObject();
        var guide = source?.DeepClone().AsObject() ?? new JsonObject();
        var headers = new JsonObject();
        var notes = guide["notes"] as JsonArray ?? new JsonArray();
        guide["notes"] = notes;
        var authorized = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any();
        guide["access"] = path.StartsWith("/api/v1/admin/") ? "Solo administradores." : authorized ? "Requiere iniciar sesión." : "No requiere una clave de acceso.";

        if (authorized)
            headers["Authorization"] = "Bearer TU_ACCESS_TOKEN";

        if (operation.RequestBody?.Content?.TryGetValue("application/json", out var media) == true)
        {
            if (guide["example"] is null)
                throw new InvalidOperationException($"Falta un ejemplo JSON para {method} {path}.");
            headers["Content-Type"] = "application/json";
            media.Example = guide["example"]!.DeepClone();
            operation.RequestBody.Description = string.Join("\n", guide["fields"]!.AsObject().Select(f => $"{f.Key}: {f.Value}"));
        }
        else
            notes.Add("Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.");

        if (method == "POST" && path == "/api/v1/levels")
            AddHeader("Idempotency-Key", "33333333-3333-4333-8333-333333333333", "UUID nuevo al crear un nivel; conserva la misma clave al reintentar.");

        if ((method == "PUT" && path.EndsWith("/draft")) || (method == "POST" && path.EndsWith("/publish")))
            AddHeader("If-Match", "\"1\"", "ETag del borrador más reciente, con comillas. No uses siempre el valor de ejemplo.");

        if (path is "/api/v1/auth/login" or "/api/v1/auth/refresh" or "/api/v1/auth/logout")
        {
            headers["Origin"] = "https://TU-DOMINIO-PERMITIDO";
            notes.Add("Origin debe coincidir con un dominio permitido por el servidor. El navegador lo envía automáticamente. En clientes externos debes enviarlo tú.");

            if (!path.EndsWith("/login"))
                notes.Add("Envía la cookie gd_refresh recibida al iniciar sesión. En el navegador se envía automáticamente; otros clientes deben conservar las cookies entre solicitudes.");
        }
        guide["headers"] = headers;

        foreach (var parameter in operation.Parameters ?? [])
        {
            if (parameter.In == ParameterLocation.Path)
                parameter.Description = path.Contains("/admin/users/") ? "UUID de la cuenta que vas a moderar." : path.Contains("/revisions/") ? "revisionId de la versión del nivel que quieres valorar." : path.StartsWith("/api/v1/attempts/") ? "id de la partida que devolvió su creación." : "levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.";

            if (parameter.In == ParameterLocation.Query)
            {
                parameter.Description = parameter.Name switch
                {
                    "page" => "Página a consultar: de 1 a 1000000. Si se omite, 1.",
                    "pageSize" => "Elementos por página: de 1 a 100. Si se omite, 20.",
                    "search" => "Parte del título del nivel, hasta 80 caracteres.",
                    "difficulty" => "Easy, Normal o Hard.",
                    "authorId" => "UUID del creador para mostrar solo sus niveles.",
                    "sort" => "recent, rating o completed. Si se omite, recent.",
                    "speed" => "Velocidad: 0.75, 1, 1.25 o 1.5.",
                    _ => parameter.Description
                };

                if (parameter is OpenApiParameter editable)
                    editable.Example = parameter.Name switch
                    {
                        "page" => JsonValue.Create(1),
                        "pageSize" => JsonValue.Create(20),
                        "search" => JsonValue.Create("recorrido"),
                        "difficulty" => JsonValue.Create("Normal"),
                        "sort" => JsonValue.Create("recent"),
                        "speed" => JsonValue.Create(1),
                        "authorId" => JsonValue.Create("44444444-4444-4444-8444-444444444444"),
                        _ => null
                    };
            }
        }
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions["x-request-guide"] = new JsonNodeExtension(guide);

        return Task.CompletedTask;

        void AddHeader(string name, string example, string description)
        {
            headers[name] = example;
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = name,
                In = ParameterLocation.Header,
                Required = true,
                Description = description,
                Example = JsonValue.Create(example),
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }
    }
}
