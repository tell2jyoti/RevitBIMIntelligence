using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitBIMIntelligence.Services;

namespace RevitBIMIntelligence.UI;

/// <summary>
/// Code-behind for the ChatPanel user control
/// </summary>
public partial class ChatPanel : UserControl, INotifyPropertyChanged
{
    private readonly ChatbotService _chatbotService;
    private readonly ObservableCollection<ChatMessage> _messages;
    private bool _isProcessing;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ChatPanel()
    {
        InitializeComponent();
        _chatbotService = new ChatbotService();
        _messages = new ObservableCollection<ChatMessage>();
        MessagesPanel.ItemsSource = _messages;

        // Add welcome message
        AddMessage("AI Assistant", "Hello! I'm your BIM assistant. Ask me questions about the building model, such as:\n\n" +
            "- \"How many rooms are on Level 2?\"\n" +
            "- \"Which level has the most doors?\"\n" +
            "- \"List all rooms with area less than 20 sqm\"\n" +
            "- \"Which rooms have no windows?\"", false);

        StatusBar.Visibility = Visibility.Collapsed;
    }

    private void AddMessage(string sender, string message, bool isUser)
    {
        var chatMessage = new ChatMessage
        {
            Sender = sender,
            Message = message,
            IsUser = isUser,
            Timestamp = DateTime.Now
        };

        _messages.Add(chatMessage);

        // Scroll to bottom
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ChatScrollViewer.ScrollToEnd();
        }));
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendMessage();
    }

    private async void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !_isProcessing)
        {
            await SendMessage();
        }
    }

    private async Task SendMessage()
    {
        var userMessage = InputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(userMessage) || _isProcessing)
            return;

        _isProcessing = true;
        SendButton.IsEnabled = false;
        InputTextBox.Text = "";
        StatusBar.Visibility = Visibility.Visible;
        StatusText.Text = "Thinking...";

        // Add user message
        AddMessage("You", userMessage, true);

        try
        {
            // Get response from chatbot
            var response = await _chatbotService.GetResponseAsync(userMessage);
            AddMessage("AI Assistant", response, false);
        }
        catch (Exception ex)
        {
            AddMessage("AI Assistant", $"Sorry, I encountered an error: {ex.Message}", false);
        }
        finally
        {
            _isProcessing = false;
            SendButton.IsEnabled = true;
            StatusBar.Visibility = Visibility.Collapsed;
        }
    }
}

/// <summary>
/// Represents a chat message
/// </summary>
public class ChatMessage
{
    public string Sender { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }

    public string Background => IsUser ? "#E3F2FD" : "White";
    public HorizontalAlignment Alignment => IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public Thickness Margin => IsUser ? new Thickness(40, 4, 8, 4) : new Thickness(8, 4, 40, 4);
}
