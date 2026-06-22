namespace Felinoir.Infrastructure.Felix;

/// <summary>
/// Locates <c>cinemas.yml</c> by walking up from the working directory and the app's
/// base directory until found. This mirrors how <c>.env</c> is discovered, so the file
/// resolves whether the app is launched from the repo root or the API project folder.
/// </summary>
internal static class CinemasConfigLocator
{
    public static string Locate(string fileName = "cinemas.yml")
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate)) return candidate;
            }
        }

        // Not found — return the bare name; the provider treats a missing file as empty config.
        return fileName;
    }
}
