using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMIntelligence.Services;

namespace RevitBIMIntelligence.Commands;

/// <summary>
/// Command to toggle the visibility of the AI chatbot panel
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
public class ToggleChatPanelCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var uiApp = commandData.Application;

            // Initialize RealTimeDataManager for live Revit API calls from chatbot
            // This subscribes to Idling event for thread-safe API access
            RealTimeDataManager.Initialize(uiApp);

            var chatPane = App.GetChatPane(uiApp);

            if (chatPane == null)
            {
                message = "Chat panel is not registered. Please restart Revit.";
                return Result.Failed;
            }

            // Toggle visibility
            if (chatPane.IsShown())
            {
                chatPane.Hide();
            }
            else
            {
                chatPane.Show();
            }

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = $"Error toggling chat panel: {ex.Message}";
            return Result.Failed;
        }
    }
}
