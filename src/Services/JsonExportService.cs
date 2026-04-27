using System.IO;
using System.Text.Json;
using RevitBIMIntelligence.Models;

namespace RevitBIMIntelligence.Services;

/// <summary>
/// Service for exporting room data to JSON files
/// </summary>
public class JsonExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Exports room data to a JSON file in the output directory
    /// </summary>
    public string ExportToJson(RoomDataExport export, string? customPath = null)
    {
        var outputPath = customPath ?? GetDefaultOutputPath();

        // Ensure directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(export, JsonOptions);
        File.WriteAllText(outputPath, json);

        return outputPath;
    }

    /// <summary>
    /// Gets the default output path for JSON export
    /// </summary>
    private string GetDefaultOutputPath()
    {
        // Try to use the plugin's output folder first
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? "";

        // Go up to find the project root and use output folder
        var outputDir = Path.Combine(assemblyDir, "output");

        // If that doesn't exist, try Documents folder
        if (!Directory.Exists(outputDir))
        {
            outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "RevitBIMIntelligence",
                "output"
            );
        }

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        return Path.Combine(outputDir, "rooms.json");
    }

    /// <summary>
    /// Loads room data from a JSON file
    /// </summary>
    public RoomDataExport? LoadFromJson(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<RoomDataExport>(json, JsonOptions);
    }
}
