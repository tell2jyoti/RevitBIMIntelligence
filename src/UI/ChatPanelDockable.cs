using System.Windows;
using Autodesk.Revit.UI;

namespace RevitBIMIntelligence.UI;

/// <summary>
/// Provides the dockable pane implementation for the chat panel
/// </summary>
public class ChatPanelDockable : IDockablePaneProvider
{
    private ChatPanel? _chatPanel;

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        _chatPanel = new ChatPanel();
        data.FrameworkElement = _chatPanel;

        // Set initial state
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right,
            MinimumWidth = 300,
            MinimumHeight = 400
        };

        data.VisibleByDefault = false;
    }

    public ChatPanel? GetChatPanel() => _chatPanel;
}
