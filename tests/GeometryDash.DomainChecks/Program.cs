using System.Text.Json;
using GeometryDash.Api.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var tests = 0;
void Check(string name, bool condition) { if (!condition) throw new Exception("FAIL: " + name); tests++; Console.WriteLine("OK: " + name); }
Dictionary<string, string[]> Validate(LevelDocument doc, bool publish = true) => LevelRules.Validate("Mi nivel", "", "Normal", doc, publish);
LevelObject Cube(string id, double x, double y = 0) => new(id, "cube", x, y, 1, 1, 0);
var empty = LevelDocument.Empty;
var valid = empty with { Objects = [Cube("a", 14), new("b", "triangle", 20, 0, 1, 1, 0), new("c", "platform", 26, 2, 4, 0.5, 0)] };
Check("Borrador vacío permitido", Validate(empty, false).Count == 0);
Check("No publicar vacío", Validate(empty).ContainsKey("content.objects"));
Check("Documento de ejemplo válido", Validate(valid).Count == 0);
Check("Rechazar versión desconocida", Validate(valid with { PhysicsVersion = 2 }).Count > 0);
Check("Rechazar longitud corta", Validate(valid with { Length = 99 }).Count > 0);
Check("Rechazar velocidad arbitraria", Validate(valid with { SpeedMultiplier = 2 }).Count > 0);
Check("Rechazar meta antes del inicio", Validate(valid with { FinishX = 1 }).Count > 0);
Check("Rechazar inicio elevado", Validate(valid with { Spawn = new(2, 1) }).Count > 0);
Check("Rechazar tipo desconocido", Validate(valid with { Objects = [Cube("a", 14) with { Type = "script" }] }).Count > 0);
Check("Rechazar ID duplicado", Validate(valid with { Objects = [Cube("a", 14), Cube("a", 20)] }).Count > 0);
Check("Rechazar fuera de cuadrícula", Validate(valid with { Objects = [Cube("a", 14.1)] }).Count > 0);
Check("Rechazar tamaño incorrecto", Validate(valid with { Objects = [Cube("a", 14) with { Width = 2 }] }).Count > 0);
Check("Rechazar zona de inicio", Validate(valid with { Objects = [Cube("a", 3)] }).Count > 0);
Check("Rechazar zona de meta", Validate(valid with { Objects = [Cube("a", 297)] }).Count > 0);
Check("Rechazar intersección de sólidos", Validate(valid with { Objects = [Cube("a", 14), Cube("b", 14.5)] }).Count > 0);
Check("Permitir bordes contiguos", Validate(valid with { Objects = [Cube("a", 14), Cube("b", 15)] }).Count == 0);
Check("Permitir cubo sobre plataforma", Validate(valid with { Objects = [Cube("a", 26, 2.5), new("b", "platform", 26, 2, 4, 0.5, 0)] }).Count == 0);
Check("Rechazar triángulo dentro de sólido", Validate(valid with { Objects = [Cube("a", 14), new("b", "triangle", 14, 0, 1, 1, 0)] }).Count > 0);
Check("Rechazar 2001 objetos", Validate(valid with { Objects = Enumerable.Range(0, 2001).Select(i => Cube(i.ToString(), i + 10)).ToArray() }).Count > 0);
Check("Rechazar NaN sin excepción", Validate(valid with { SpeedMultiplier = double.NaN }).Count > 0);
Check("Hash estable sin depender del orden", LevelRules.Hash(LevelRules.Canonical(valid)) == LevelRules.Hash(LevelRules.Canonical(valid with { Objects = valid.Objects.Reverse().ToArray() })));
Check("Hash cambia con contenido", LevelRules.Hash(LevelRules.Canonical(valid)) != LevelRules.Hash(LevelRules.Canonical(valid with { SpeedMultiplier = 1.5 })));
var unknown = LevelRules.Canonical(valid).Replace("\"rotation\":0", "\"rotation\":0,\"script\":true");
var rejected = false;
try { JsonSerializer.Deserialize<LevelDocument>(unknown, LevelRules.Json); } catch (JsonException) { rejected = true; }
Check("Rechazar propiedades desconocidas", rejected);
var completed = new AttemptResult(Guid.NewGuid(), "Completed", 5920, 0, 100);
Check("Completar con ticks suficientes", AttemptRules.Validate(completed, valid, 50) is null);
Check("No aceptar 100% con cero ticks", AttemptRules.Validate(completed with { ElapsedTicks = 0 }, valid, 50) is not null);
Check("No aceptar tiempo imposible", AttemptRules.Validate(completed, valid, 1) is not null);
Check("No aceptar muerte con 100%", AttemptRules.Validate(completed with { Outcome = "Dead" }, valid, 50) is not null);
Check("No aceptar ticks negativos", AttemptRules.Validate(completed with { ElapsedTicks = -1 }, valid, 50) is not null);
Check("Aceptar abandono sin avance", AttemptRules.Validate(new(Guid.NewGuid(), "Quit", 0, 0, 0), valid, 1) is null);
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../GeometryDash.Api"));
using var requestDocs = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Documentation/requests.json")));
var draftExample = requestDocs.RootElement.GetProperty("PUT /api/v1/levels/{id}/draft").GetProperty("example");
var exampleDoc = draftExample.GetProperty("content").Deserialize<LevelDocument>(LevelRules.Json)!;
Check("JSON documentado permite guardar y publicar el recorrido", LevelRules.Validate(draftExample.GetProperty("title").GetString()!, draftExample.GetProperty("description").GetString()!, draftExample.GetProperty("difficulty").GetString()!, exampleDoc, true).Count == 0);
var resultExample = requestDocs.RootElement.GetProperty("PUT /api/v1/attempts/{id}/result").GetProperty("example").Deserialize<AttemptResult>(LevelRules.Json)!;
Check("JSON documentado de resultado respeta las reglas de partida", AttemptRules.Validate(resultExample, exampleDoc, 1) is null);
Console.WriteLine($"{tests} comprobaciones aprobadas.");
var sourceFiles = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)).ToArray();
foreach (var file in sourceFiles)
{
    var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
    var errors = syntax.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

    if (errors.Length > 0)
        throw new Exception(file + ": " + string.Join("; ", errors.Select(e => e.ToString())));
}
Console.WriteLine($"Sintaxis C# válida en {sourceFiles.Length} archivos. La comprobación semántica de la API requiere restaurar NuGet.");
