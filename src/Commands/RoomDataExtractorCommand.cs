using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMIntelligence.Models;
using RevitBIMIntelligence.Services;

namespace RevitBIMIntelligence.Commands;

/// <summary>
/// Revit external command that extracts room data and displays it in a WPF panel
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public class RoomDataExtractorCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var uiApp = commandData.Application;
            var doc = uiApp.ActiveUIDocument?.Document;

            if (doc == null)
            {
                message = "No active document found. Please open a Revit project.";
                return Result.Failed;
            }

            // Extract room data
            var roomDataService = new RoomDataService();
            var rooms = roomDataService.ExtractRoomData(doc);

            if (rooms.Count == 0)
            {
                TaskDialog.Show("Room Data Extractor",
                    "No rooms found in the current document.\n\n" +
                    "Make sure the document contains placed rooms with area.");
                return Result.Succeeded;
            }

            // Store for use by chatbot and other services
            AppState.CurrentRoomData = rooms;
            AppState.CurrentDocument = doc;

            // Export to JSON
            var jsonService = new JsonExportService();
            var export = roomDataService.CreateExport(doc, rooms);
            var jsonPath = jsonService.ExportToJson(export);

            // Show the room data panel
            App.ShowRoomDataPanel(uiApp, rooms);

            TaskDialog.Show("Room Data Extractor",
                $"Successfully extracted {rooms.Count} rooms.\n\n" +
                $"JSON exported to:\n{jsonPath}");

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = $"Error extracting room data: {ex.Message}";
            return Result.Failed;
        }
    }
}

/// <summary>
/// Static class to hold application state shared between components
/// </summary>
public static class AppState
{
    public static List<RoomData>? CurrentRoomData { get; set; }
    public static Document? CurrentDocument { get; set; }
}
