namespace SoundCloudDigger.Api;

public static class EnvFileLoader
{
    public static void Load()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(AppContext.BaseDirectory, ".env"),
        };

        var envFile = candidates.FirstOrDefault(File.Exists);
        if (envFile is null) return;

        foreach (var line in File.ReadAllLines(envFile))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex < 1) continue;

            var key = trimmed[..eqIndex].Trim();
            var value = trimmed[(eqIndex + 1)..].Trim();

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
