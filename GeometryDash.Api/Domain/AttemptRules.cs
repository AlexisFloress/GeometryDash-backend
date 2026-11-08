namespace GeometryDash.Api.Domain;
public sealed record AttemptResult(Guid ClientResultId, string Outcome, long ElapsedTicks, long PauseTicks, decimal ProgressPercent);
public static class AttemptRules
{
    public static string? Validate(AttemptResult r, LevelDocument doc, double wallSeconds)
    {
        if (r.ClientResultId == Guid.Empty || r.Outcome is not ("Dead" or "Completed" or "Quit") || r.ElapsedTicks is < 0 or > 216000 || r.PauseTicks is < 0 or > 216000 || r.ProgressPercent is < 0 or > 100) 
            return "Resultado fuera de límites.";

        if (r.ElapsedTicks + r.PauseTicks > (wallSeconds + 3) * 120) 
            return "Los ticks exceden el tiempo disponible.";

        var expected = Math.Clamp(r.ElapsedTicks / 120d * 6 * doc.SpeedMultiplier / (doc.FinishX - doc.Spawn.X) * 100, 0, 100);

        if (Math.Abs((double)r.ProgressPercent - expected) > 1) 
            return "Avance incoherente con velocidad y ticks.";

        if (r.Outcome == "Completed" && (r.ProgressPercent != 100 || expected < 100)) 
            return "Completar requiere alcanzar la meta.";

        if (r.Outcome != "Completed" && r.ProgressPercent >= 100) 
            return "Solo un completado puede tener 100%.";


        return null;

    }
}
