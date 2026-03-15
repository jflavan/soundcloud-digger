using Microsoft.AspNetCore.Mvc;

namespace SoundCloudDigger.Api.Controllers;

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly IConfiguration _config;

    public SetupController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var clientId = _config["SoundCloud:ClientId"];
        var clientSecret = _config["SoundCloud:ClientSecret"];
        var configured = !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret);
        return Ok(new { configured });
    }

    [HttpPost("credentials")]
    public IActionResult SaveCredentials([FromBody] CredentialsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
            return BadRequest(new { error = "Both clientId and clientSecret are required." });

        // Set env vars on the running process so config picks them up
        Environment.SetEnvironmentVariable("SoundCloud__ClientId", request.ClientId);
        Environment.SetEnvironmentVariable("SoundCloud__ClientSecret", request.ClientSecret);

        // Write to .env file for persistence across restarts
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        var lines = new List<string>();

        if (System.IO.File.Exists(envPath))
        {
            foreach (var line in System.IO.File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("SoundCloud__ClientId=") || trimmed.StartsWith("SoundCloud__ClientSecret="))
                    continue;
                lines.Add(line);
            }
        }

        lines.Add($"SoundCloud__ClientId={request.ClientId}");
        lines.Add($"SoundCloud__ClientSecret={request.ClientSecret}");
        System.IO.File.WriteAllLines(envPath, lines);

        // Reload configuration
        if (_config is IConfigurationRoot configRoot)
            configRoot.Reload();

        return Ok(new { success = true });
    }

    public record CredentialsRequest(string ClientId, string ClientSecret);
}
