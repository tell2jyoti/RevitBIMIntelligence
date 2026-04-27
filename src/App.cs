using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using RevitBIMIntelligence.Models;
using RevitBIMIntelligence.Services;
using RevitBIMIntelligence.UI;

namespace RevitBIMIntelligence;

/// <summary>
/// Main application entry point for the Revit plugin
/// Implements IExternalApplication to set up ribbon UI on startup
/// </summary>
public class App : IExternalApplication
{
    // Dockable pane IDs
    public static readonly Guid ChatPanelGuid = new("D3E4F5A6-B7C8-9012-ABCD-EF3456789012");
    public static DockablePaneId ChatPanelId => new(ChatPanelGuid);

    // Static reference to UIApplication for use by other components
    private static UIControlledApplication? _uiControlledApp;
    private static UIApplication? _uiApp;

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            _uiControlledApp = application;

            // Create ribbon tab
            CreateRibbonTab(application);

            // Register dockable pane for chat
            RegisterDockablePanes(application);

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("BIM Intelligence Error",
                $"Failed to initialize plugin: {ex.Message}");
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }

    private void CreateRibbonTab(UIControlledApplication application)
    {
        var tabName = "BIM Intelligence";

        // Create the tab
        application.CreateRibbonTab(tabName);

        // Create panel
        var panel = application.CreateRibbonPanel(tabName, "Room Data");

        // Get assembly path
        var assemblyPath = Assembly.GetExecutingAssembly().Location;

        // Room Data Extractor button
        var extractorButtonData = new PushButtonData(
            "RoomDataExtractor",
            "Extract\nRoom Data",
            assemblyPath,
            "RevitBIMIntelligence.Commands.RoomDataExtractorCommand")
        {
            ToolTip = "Extract room data from the current model including room names, numbers, levels, areas, doors, and windows.",
            LongDescription = "Extracts comprehensive room data from the active Revit document and displays it in a data grid. The data can also be exported to JSON format."
        };

        var extractorButton = panel.AddItem(extractorButtonData) as PushButton;

        // Try to set button image (optional)
        try
        {
            var iconUri = new Uri("pack://application:,,,/RevitBIMIntelligence;component/Resources/room_icon.png");
            extractorButton!.LargeImage = new BitmapImage(iconUri);
        }
        catch
        {
            // Icon not found, button will use default
        }

        // Create AI panel
        var aiPanel = application.CreateRibbonPanel(tabName, "AI Assistant");

        // Chat Panel toggle button
        var chatButtonData = new PushButtonData(
            "ChatPanel",
            "AI\nChatbot",
            assemblyPath,
            "RevitBIMIntelligence.Commands.ToggleChatPanelCommand")
        {
            ToolTip = "Open the AI chatbot panel to ask questions about the building model.",
            LongDescription = "Opens a dockable chat panel where you can ask natural language questions about the building model. The AI will query the Revit model in real-time to answer your questions."
        };

        var chatButton = aiPanel.AddItem(chatButtonData) as PushButton;

        // Try to set button image
        try
        {
            var iconUri = new Uri("pack://application:,,,/RevitBIMIntelligence;component/Resources/chat_icon.png");
            chatButton!.LargeImage = new BitmapImage(iconUri);
        }
        catch
        {
            // Icon not found, button will use default
        }
    }

    private void RegisterDockablePanes(UIControlledApplication application)
    {
        // Register the chat panel as a dockable pane
        var chatPanelProvider = new ChatPanelDockable();
        application.RegisterDockablePane(ChatPanelId, "AI Chatbot", chatPanelProvider);
    }

    /// <summary>
    /// Shows the room data panel with extracted data
    /// </summary>
    public static void ShowRoomDataPanel(UIApplication uiApp, List<RoomData> roomData)
    {
        _uiApp = uiApp;
        var panel = new RoomDataPanel(roomData);
        panel.ShowDialog();
    }

    /// <summary>
    /// Gets the dockable chat pane
    /// </summary>
    public static DockablePane? GetChatPane(UIApplication uiApp)
    {
        try
        {
            return uiApp.GetDockablePane(ChatPanelId);
        }
        catch
        {
            return null;
        }
    }
}
