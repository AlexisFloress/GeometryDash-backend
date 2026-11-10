using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeometryDash.Api.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Point(double X, double Y);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LevelObject(string Id, string Type, double X, double Y, double Width, double Height, double Rotation);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LevelDocument(int SchemaVersion, int PhysicsVersion, int Length, double SpeedMultiplier,
    Point Spawn, double FinishX, LevelObject[] Objects)
{
    public static LevelDocument Empty => new(1, 1, 300, 1, new(2, 0), 298, []);
}

public static class LevelRules
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Dictionary<string, string[]> Validate(string title, string description, string difficulty, LevelDocument? doc, bool publish)
    {
        var errors = new Dictionary<string, string[]>();
        void Error(string path, string text) => errors[path] = [text];

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length is < 3 or > 80)
            Error("title", "Usa entre 3 y 80 caracteres.");

        if (description is null || description.Length > 2000)
            Error("description", "Máximo 2000 caracteres.");

        if (difficulty is not ("Easy" or "Normal" or "Hard"))
            Error("difficulty", "Dificultad no admitida.");

        if (doc is null)
        {
            Error("content", "Documento requerido.");

            return errors;
        }

        if (doc.SchemaVersion != 1 || doc.PhysicsVersion != 1)
            Error("content.schemaVersion", "Solo se admite esquema y física 1.");

        if (doc.Length is < 100 or > 5000)
            Error("content.length", "Longitud entre 100 y 5000.");

        if (doc.SpeedMultiplier is not (0.75 or 1 or 1.25 or 1.5))
            Error("content.speedMultiplier", "Velocidad no admitida.");

        if (doc.Spawn is null || !Grid(doc.Spawn.X) || doc.Spawn.X < 0 || doc.Spawn.X > 10 || doc.Spawn.Y != 0)
            Error("content.spawn", "Inicio sobre el piso, entre x=0 y x=10, en cuadrícula de 0.5.");

        if (!Grid(doc.FinishX) || doc.FinishX > doc.Length || doc.FinishX < (doc.Spawn?.X ?? 0) + 10)
            Error("content.finishX", "Meta inválida.");

        if (doc.Objects is null)
        {
            Error("content.objects", "Lista requerida.");

            return errors;
        }

        if (doc.Objects.Length > 2000 || (publish && doc.Objects.Length == 0))
            Error("content.objects", "Publicar requiere de 1 a 2000 objetos; el borrador permite cero.");

        try
        {
            if (JsonSerializer.SerializeToUtf8Bytes(doc, Json).Length > 512 * 1024)
                Error("content", "Máximo 512 KiB.");
        }
        catch (ArgumentException) { Error("content", "Los números deben ser finitos."); }
        var ids = new HashSet<string>();

        for (var i = 0; i < Math.Min(doc.Objects.Length, 2000); i++)
        {
            var o = doc.Objects[i];
            var p = $"content.objects[{i}]";

            if (o is null)
            {
                Error(p, "Objeto requerido.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(o.Id) || o.Id.Length > 64 || !ids.Add(o.Id))
                Error(p + ".id", "Identificador vacío, largo o duplicado.");

            if (o.Type is not ("cube" or "triangle" or "platform"))
                Error(p + ".type", "Tipo desconocido.");

            if (!Grid(o.X) || !Grid(o.Y) || !Grid(o.Width) || !Grid(o.Height) || o.X < 0 || o.Y < 0 || o.X + o.Width > doc.Length || o.Y + o.Height > 30)
                Error(p, "Objeto fuera del recorrido o de la cuadrícula de 0.5.");

            if (o.Rotation != 0 || (o.Type == "platform" ? o.Width is < 1 or > 10 || o.Height != 0.5 : o.Width != 1 || o.Height != 1))
                Error(p + ".width", "Tamaño o rotación no admitidos.");

            if (o.X < (doc.Spawn?.X ?? 0) + 4 || o.X + o.Width > doc.FinishX - 2)
                Error(p + ".x", "Reserva cuatro unidades tras el inicio y dos antes de la meta.");
            // Intersección de interiores de sólidos y triángulo/sólido; triángulo/triángulo se admite.
            for (var j = 0; j < i; j++)
            {
                var b = doc.Objects[j];

                if (b is null || (o.Type == "triangle" && b.Type == "triangle"))
                    continue;

                if (Overlap(o, b))
                {
                    Error(p, "Interseca el interior de otro objeto.");
                    break;
                }
            }
        }

        return errors;
    }

    private static bool Grid(double x) => double.IsFinite(x) && x * 2 == Math.Truncate(x * 2);

    private static bool Overlap(LevelObject a, LevelObject b)
    {
        if (!(a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height))
            return false;

        if (a.Type != "triangle" && b.Type != "triangle")
            return true;
        var t = a.Type == "triangle" ? a : b;
        var r = a.Type == "triangle" ? b : a;
        var bottom = Math.Max(t.Y, r.Y);
        var top = Math.Min(t.Y + t.Height, r.Y + r.Height);

        if (bottom >= top)
            return false;
        var inset = (bottom - t.Y) / t.Height * t.Width / 2;

        return Math.Max(t.X + inset, r.X) < Math.Min(t.X + t.Width - inset, r.X + r.Width);
    }

    public static string Canonical(LevelDocument doc) => JsonSerializer.Serialize(doc with { Objects = doc.Objects.OrderBy(o => o.Id, StringComparer.Ordinal).ToArray() }, Json);

    public static string Hash(string canonical) => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
}
