using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitBIMIntelligence.Commands;
using RevitBIMIntelligence.Models;

namespace RevitBIMIntelligence.Services;

/// <summary>
/// Manages real-time room data extraction using Revit's Idling event
/// This ensures Revit API calls happen on the main thread while supporting async chatbot
/// </summary>
public static class RealTimeDataManager
{
    private static UIApplication? _uiApp;
    private static RoomDataService? _roomDataService;
    private static TaskCompletionSource<List<RoomData>>? _pendingRequest;
    private static bool _isInitialized = false;

    /// <summary>
    /// Initialize the manager with UIApplication (call from a command context)
    /// </summary>
    public static void Initialize(UIApplication uiApp)
    {
        if (_isInitialized) return;

        _uiApp = uiApp;
        _roomDataService = new RoomDataService();

        // Subscribe to Idling event - this fires when Revit is ready to process
        _uiApp.Idling += OnIdling;
        _isInitialized = true;
    }

    /// <summary>
    /// Shutdown and cleanup
    /// </summary>
    public static void Shutdown()
    {
        if (_uiApp != null && _isInitialized)
        {
            _uiApp.Idling -= OnIdling;
            _isInitialized = false;
        }
    }

    /// <summary>
    /// Request fresh room data extraction - called from async chatbot
    /// </summary>
    public static Task<List<RoomData>> ExtractRoomDataAsync()
    {
        // If not initialized, return empty
        if (!_isInitialized || _uiApp == null)
        {
            return Task.FromResult(new List<RoomData>());
        }

        // Create a new task completion source for this request
        _pendingRequest = new TaskCompletionSource<List<RoomData>>();
        return _pendingRequest.Task;
    }

    /// <summary>
    /// Called by Revit on the main thread when idle
    /// This is where we safely execute Revit API calls
    /// </summary>
    private static void OnIdling(object? sender, IdlingEventArgs e)
    {
        // Check if there's a pending extraction request
        if (_pendingRequest == null || _roomDataService == null || _uiApp == null)
            return;

        var request = _pendingRequest;
        _pendingRequest = null; // Clear the request

        try
        {
            var doc = _uiApp.ActiveUIDocument?.Document;

            if (doc == null)
            {
                request.TrySetResult(new List<RoomData>());
                return;
            }

            // Extract fresh data from the live model RIGHT NOW
            var rooms = _roomDataService.ExtractRoomData(doc);

            // Update AppState with fresh data
            AppState.CurrentRoomData = rooms;
            AppState.CurrentDocument = doc;

            // Return the fresh data to the waiting async task
            request.TrySetResult(rooms);
        }
        catch (Exception ex)
        {
            request.TrySetException(ex);
        }
    }

    /// <summary>
    /// Check if manager is ready to handle requests
    /// </summary>
    public static bool IsInitialized => _isInitialized;
}

/// <summary>
/// Legacy ExternalEventManager - kept for compatibility but now wraps RealTimeDataManager
/// </summary>
public static class ExternalEventManager
{
    public static void Initialize()
    {
        // No-op - initialization now happens via RealTimeDataManager
    }

    public static async Task<List<RoomData>> ExtractRoomDataAsync()
    {
        if (!RealTimeDataManager.IsInitialized)
        {
            // Not initialized yet, return cached data if available
            return AppState.CurrentRoomData ?? new List<RoomData>();
        }

        try
        {
            // Use RealTimeDataManager with timeout
            var task = RealTimeDataManager.ExtractRoomDataAsync();
            var completedTask = await Task.WhenAny(task, Task.Delay(5000)).ConfigureAwait(false);

            if (completedTask == task)
            {
                return await task.ConfigureAwait(false);
            }
            else
            {
                // Timeout - return cached data
                return AppState.CurrentRoomData ?? new List<RoomData>();
            }
        }
        catch
        {
            return AppState.CurrentRoomData ?? new List<RoomData>();
        }
    }
}
