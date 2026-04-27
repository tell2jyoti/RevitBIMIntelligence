using System.Text.Json.Serialization;

namespace RevitBIMIntelligence.Models;

/// <summary>
/// Represents extracted room data from a Revit model
/// </summary>
public class RoomData
{
    /// <summary>
    /// Unique Revit Element ID of the room
    /// </summary>
    [JsonPropertyName("elementId")]
    public int ElementId { get; set; }

    /// <summary>
    /// Room name from Revit
    /// </summary>
    [JsonPropertyName("roomName")]
    public string RoomName { get; set; } = string.Empty;

    /// <summary>
    /// Room number from Revit
    /// </summary>
    [JsonPropertyName("roomNumber")]
    public string RoomNumber { get; set; } = string.Empty;

    /// <summary>
    /// Level name where the room is located
    /// </summary>
    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Room area in square meters
    /// </summary>
    [JsonPropertyName("areaSquareMeters")]
    public double AreaSquareMeters { get; set; }

    /// <summary>
    /// Number of doors in/around the room
    /// </summary>
    [JsonPropertyName("doorCount")]
    public int DoorCount { get; set; }

    /// <summary>
    /// Number of windows in/around the room
    /// </summary>
    [JsonPropertyName("windowCount")]
    public int WindowCount { get; set; }

    public override string ToString()
    {
        return $"{RoomName} ({RoomNumber}) - Level: {Level}, Area: {AreaSquareMeters:F2} sqm, Doors: {DoorCount}, Windows: {WindowCount}";
    }
}

/// <summary>
/// Container for exporting room data to JSON
/// </summary>
public class RoomDataExport
{
    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("exportDate")]
    public DateTime ExportDate { get; set; } = DateTime.Now;

    [JsonPropertyName("totalRooms")]
    public int TotalRooms => Rooms?.Count ?? 0;

    [JsonPropertyName("rooms")]
    public List<RoomData> Rooms { get; set; } = new();
}
