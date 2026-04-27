using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitBIMIntelligence.Models;

namespace RevitBIMIntelligence.Services;

/// <summary>
/// Service for extracting room data from Revit documents
/// </summary>
public class RoomDataService
{
    private const double SquareFeetToSquareMeters = 0.092903;

    /// <summary>
    /// Extracts all room data from the given Revit document
    /// </summary>
    public List<RoomData> ExtractRoomData(Document doc)
    {
        var rooms = new List<RoomData>();

        // Get all rooms using FilteredElementCollector
        var roomCollector = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType();

        // Pre-collect all doors and windows for efficiency
        var allDoors = GetAllFamilyInstances(doc, BuiltInCategory.OST_Doors);
        var allWindows = GetAllFamilyInstances(doc, BuiltInCategory.OST_Windows);

        foreach (var element in roomCollector)
        {
            if (element is Room room && room.Area > 0) // Skip unplaced rooms
            {
                var roomData = ExtractSingleRoomData(room, allDoors, allWindows);
                rooms.Add(roomData);
            }
        }

        return rooms.OrderBy(r => r.Level).ThenBy(r => r.RoomNumber).ToList();
    }

    /// <summary>
    /// Extracts data from a single room
    /// </summary>
    private RoomData ExtractSingleRoomData(
        Room room,
        List<FamilyInstance> allDoors,
        List<FamilyInstance> allWindows)
    {
        var roomData = new RoomData
        {
            ElementId = (int)room.Id.Value,
            RoomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Unnamed",
            RoomNumber = room.Number ?? "N/A",
            Level = room.Level?.Name ?? "Unknown",
            AreaSquareMeters = Math.Round(room.Area * SquareFeetToSquareMeters, 2),
            DoorCount = CountDoorsForRoom(room, allDoors),
            WindowCount = CountWindowsForRoom(room, allWindows)
        };

        return roomData;
    }

    /// <summary>
    /// Gets all family instances of a specific category
    /// </summary>
    private List<FamilyInstance> GetAllFamilyInstances(Document doc, BuiltInCategory category)
    {
        return new FilteredElementCollector(doc)
            .OfCategory(category)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .ToList();
    }

    /// <summary>
    /// Counts doors associated with a room using FromRoom/ToRoom properties
    /// </summary>
    private int CountDoorsForRoom(Room room, List<FamilyInstance> allDoors)
    {
        int count = 0;
        var roomId = room.Id;

        foreach (var door in allDoors)
        {
            try
            {
                // Check if door's FromRoom or ToRoom matches this room
                var fromRoom = door.FromRoom;
                var toRoom = door.ToRoom;

                if ((fromRoom != null && fromRoom.Id == roomId) ||
                    (toRoom != null && toRoom.Id == roomId))
                {
                    count++;
                }
            }
            catch
            {
                // Some doors may not have room associations, skip them
            }
        }

        return count;
    }

    /// <summary>
    /// Counts windows associated with a room using FromRoom/ToRoom properties
    /// </summary>
    private int CountWindowsForRoom(Room room, List<FamilyInstance> allWindows)
    {
        int count = 0;
        var roomId = room.Id;

        foreach (var window in allWindows)
        {
            try
            {
                // Check if window's FromRoom or ToRoom matches this room
                var fromRoom = window.FromRoom;
                var toRoom = window.ToRoom;

                if ((fromRoom != null && fromRoom.Id == roomId) ||
                    (toRoom != null && toRoom.Id == roomId))
                {
                    count++;
                }
            }
            catch
            {
                // Some windows may not have room associations, skip them
            }
        }

        return count;
    }

    /// <summary>
    /// Creates an export container with project info
    /// </summary>
    public RoomDataExport CreateExport(Document doc, List<RoomData> rooms)
    {
        return new RoomDataExport
        {
            ProjectName = doc.Title ?? "Unknown Project",
            ExportDate = DateTime.Now,
            Rooms = rooms
        };
    }
}
