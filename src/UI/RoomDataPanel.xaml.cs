using System.Windows;
using RevitBIMIntelligence.Commands;
using RevitBIMIntelligence.Models;
using RevitBIMIntelligence.Services;

namespace RevitBIMIntelligence.UI;

/// <summary>
/// Code-behind for RoomDataPanel.xaml
/// </summary>
public partial class RoomDataPanel : Window
{
    private readonly List<RoomData> _roomData;

    public RoomDataPanel(List<RoomData> roomData)
    {
        InitializeComponent();
        _roomData = roomData;
        LoadData();
    }

    private void LoadData()
    {
        RoomDataGrid.ItemsSource = _roomData;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var totalRooms = _roomData.Count;
        var totalArea = _roomData.Sum(r => r.AreaSquareMeters);
        var totalDoors = _roomData.Sum(r => r.DoorCount);
        var totalWindows = _roomData.Sum(r => r.WindowCount);
        var levels = _roomData.Select(r => r.Level).Distinct().Count();

        SummaryText.Text = $"Total: {totalRooms} rooms across {levels} levels | " +
                          $"Area: {totalArea:N2} sqm | " +
                          $"Doors: {totalDoors} | Windows: {totalWindows}";
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var jsonService = new JsonExportService();
            var export = new RoomDataExport
            {
                ProjectName = AppState.CurrentDocument?.Title ?? "Unknown Project",
                ExportDate = DateTime.Now,
                Rooms = _roomData
            };

            var path = jsonService.ExportToJson(export);
            MessageBox.Show($"Data exported successfully to:\n{path}",
                "Export Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting data: {ex.Message}",
                "Export Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
