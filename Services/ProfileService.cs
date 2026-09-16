using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AdminWorks.Models;

namespace AdminWorks.Services
{
    public class ProfileService
    {
        public static string GetDesktopPath() => Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public async Task<string> ExportProfileAsync(IEnumerable<TweakItem> tweaks)
        {
            var profile = new Dictionary<string, bool>();
            foreach (var tweak in tweaks)
            {
                if (tweak.IsToggle)
                {
                    profile[tweak.Title] = tweak.IsActive;
                }
            }

            var filePath = Path.Combine(GetDesktopPath(), $"AdminWorks_Profile_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            return filePath;
        }

        public async Task<Dictionary<string, bool>?> LoadProfileAsync(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
        }
    }
}
