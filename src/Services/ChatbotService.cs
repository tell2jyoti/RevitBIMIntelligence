using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RevitBIMIntelligence.Commands;
using RevitBIMIntelligence.Models;

namespace RevitBIMIntelligence.Services;

/// <summary>
/// Service for handling AI chatbot interactions with LLM API
/// Uses tool calling for accurate data queries + chain-of-thought for reasoning
/// </summary>
public class ChatbotService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiUrl = "https://api.anthropic.com/v1/messages";

    // Define tools for accurate data queries
    private static readonly object[] Tools = new object[]
    {
        new
        {
            name = "count_rooms_by_level",
            description = "Count the exact number of rooms on a specific level. Use this when user asks 'how many rooms on level X'.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    level_name = new
                    {
                        type = "string",
                        description = "The level name to filter by (e.g., '01 - Entry Level', '02 - Floor', '03 - Floor'). Use partial match."
                    }
                },
                required = new[] { "level_name" }
            }
        },
        new
        {
            name = "get_building_summary",
            description = "Get overall building statistics: total rooms, total area, total doors, total windows, rooms per level. Use this for general building questions.",
            input_schema = new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "find_rooms_with_windows",
            description = "Find all rooms that have windows. Returns list of rooms with window count > 0.",
            input_schema = new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "find_rooms_without_windows",
            description = "Find all rooms that have NO windows. Returns list of rooms with window count = 0.",
            input_schema = new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "find_largest_rooms",
            description = "Find the largest rooms by area. Returns top 10 rooms sorted by area descending.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    count = new
                    {
                        type = "integer",
                        description = "Number of rooms to return (default 10)"
                    }
                },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "find_smallest_rooms",
            description = "Find the smallest rooms by area. Returns bottom 10 rooms sorted by area ascending.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    count = new
                    {
                        type = "integer",
                        description = "Number of rooms to return (default 10)"
                    }
                },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "find_rooms_by_area_range",
            description = "Find rooms within a specific area range. Use for questions like 'rooms less than 20 sqm' or 'rooms between 10 and 50 sqm'.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    min_area = new
                    {
                        type = "number",
                        description = "Minimum area in square meters (use 0 if no minimum)"
                    },
                    max_area = new
                    {
                        type = "number",
                        description = "Maximum area in square meters (use 99999 if no maximum)"
                    }
                },
                required = new[] { "min_area", "max_area" }
            }
        },
        new
        {
            name = "count_doors_by_level",
            description = "Count total doors on a specific level.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    level_name = new
                    {
                        type = "string",
                        description = "The level name to filter by"
                    }
                },
                required = new[] { "level_name" }
            }
        },
        new
        {
            name = "get_room_types_summary",
            description = "Get a breakdown of room types and their counts (e.g., how many offices, how many corridors, etc.)",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    level_name = new
                    {
                        type = "string",
                        description = "Optional: filter by level name. Leave empty for all levels."
                    }
                },
                required = Array.Empty<string>()
            }
        }
    };

    public ChatbotService()
    {
        _httpClient = new HttpClient();
        _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ??
                  Environment.GetEnvironmentVariable("CLAUDE_API_KEY") ?? "";
    }

    public async Task<string> GetResponseAsync(string userMessage)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            return "API key not configured. Please set the ANTHROPIC_API_KEY environment variable and restart Revit.";
        }

        // REAL-TIME DATA FETCHING: Extract fresh data from Revit model for EVERY question
        // Uses ExternalEvent to safely call Revit API from async context
        List<RoomData> roomData;
        try
        {
            // Try to fetch live data from the model using ExternalEvent
            roomData = await ExternalEventManager.ExtractRoomDataAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Fallback to cached data if ExternalEvent fails
            roomData = AppState.CurrentRoomData ?? new List<RoomData>();
        }

        if (roomData.Count == 0)
        {
            return "No room data available. Please click 'Extract Room Data' first to load the building data, then try your question again.";
        }

        try
        {
            // Step 1: Send user message with tools
            var messages = new List<object>
            {
                new { role = "user", content = userMessage }
            };

            var response = await CallAnthropicApi(messages);

            // Step 2: Check if tool was called
            if (response.StopReason == "tool_use")
            {
                var toolUse = response.Content.FirstOrDefault(c => c.Type == "tool_use");
                if (toolUse != null)
                {
                    // Execute the tool with OUR code (guaranteed accurate)
                    var toolResult = ExecuteTool(toolUse.Name ?? "", toolUse.Input, roomData);

                    // Add assistant response and tool result to messages
                    messages.Add(new
                    {
                        role = "assistant",
                        content = response.Content.Select(c => c.Type == "tool_use"
                            ? new { type = "tool_use", id = c.Id, name = c.Name, input = c.Input }
                            : (object)new { type = "text", text = c.Text ?? "" }).ToArray()
                    });

                    messages.Add(new
                    {
                        role = "user",
                        content = new[]
                        {
                            new
                            {
                                type = "tool_result",
                                tool_use_id = toolUse.Id,
                                content = toolResult
                            }
                        }
                    });

                    // Step 3: Get final response with tool results
                    response = await CallAnthropicApi(messages);
                }
            }

            // Extract text response
            var textContent = response.Content.FirstOrDefault(c => c.Type == "text");
            return textContent?.Text ?? "I couldn't generate a response.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private string ExecuteTool(string toolName, JsonElement? input, List<RoomData> roomData)
    {
        try
        {
            switch (toolName)
            {
                case "count_rooms_by_level":
                    return ExecuteCountRoomsByLevel(input, roomData);

                case "get_building_summary":
                    return ExecuteGetBuildingSummary(roomData);

                case "find_rooms_with_windows":
                    return ExecuteFindRoomsWithWindows(roomData);

                case "find_rooms_without_windows":
                    return ExecuteFindRoomsWithoutWindows(roomData);

                case "find_largest_rooms":
                    return ExecuteFindLargestRooms(input, roomData);

                case "find_smallest_rooms":
                    return ExecuteFindSmallestRooms(input, roomData);

                case "find_rooms_by_area_range":
                    return ExecuteFindRoomsByAreaRange(input, roomData);

                case "count_doors_by_level":
                    return ExecuteCountDoorsByLevel(input, roomData);

                case "get_room_types_summary":
                    return ExecuteGetRoomTypesSummary(input, roomData);

                default:
                    return JsonSerializer.Serialize(new { error = "Unknown tool" });
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private string ExecuteCountRoomsByLevel(JsonElement? input, List<RoomData> roomData)
    {
        var levelName = input?.GetProperty("level_name").GetString() ?? "";
        var filtered = roomData.Where(r => r.Level.Contains(levelName, StringComparison.OrdinalIgnoreCase)).ToList();

        var result = new
        {
            query = $"Rooms on level containing '{levelName}'",
            exact_count = filtered.Count,
            level_matches = filtered.Select(r => r.Level).Distinct().ToList(),
            rooms = filtered.Select(r => new { r.RoomNumber, r.RoomName, r.Level, r.AreaSquareMeters }).ToList()
        };
        return JsonSerializer.Serialize(result);
    }

    private string ExecuteGetBuildingSummary(List<RoomData> roomData)
    {
        var levels = roomData.GroupBy(r => r.Level)
            .Select(g => new
            {
                level = g.Key,
                room_count = g.Count(),
                total_area = Math.Round(g.Sum(r => r.AreaSquareMeters), 2),
                total_doors = g.Sum(r => r.DoorCount),
                total_windows = g.Sum(r => r.WindowCount)
            }).ToList();

        var result = new
        {
            total_rooms = roomData.Count,
            total_area_sqm = Math.Round(roomData.Sum(r => r.AreaSquareMeters), 2),
            total_doors = roomData.Sum(r => r.DoorCount),
            total_windows = roomData.Sum(r => r.WindowCount),
            number_of_levels = levels.Count,
            breakdown_by_level = levels
        };
        return JsonSerializer.Serialize(result);
    }

    private string ExecuteFindRoomsWithWindows(List<RoomData> roomData)
    {
        var filtered = roomData.Where(r => r.WindowCount > 0).OrderByDescending(r => r.WindowCount).ToList();
        var result = new
        {
            query = "Rooms with windows",
            exact_count = filtered.Count,
            total_windows = filtered.Sum(r => r.WindowCount),
            rooms = filtered.Select(r => new { r.RoomNumber, r.RoomName, r.Level, r.WindowCount, r.AreaSquareMeters }).ToList()
        };
        return JsonSerializer.Serialize(result);
    }

    private string ExecuteFindRoomsWithoutWindows(List<RoomData> roomData)
    {
        var filtered = roomData.Where(r => r.WindowCount == 0).ToList();
        var result = new
        {
            query = "Rooms without windows",
            exact_count = filtered.Count,
            rooms = filtered.Select(r => new { r.RoomNumber, r.RoomName, r.Level, r.AreaSquareMeters }).ToList()
        };
        return JsonSerializer.Serialize(result);
    }

    private string ExecuteFindLargestRooms(JsonElement? input, List<RoomData> roomData)
    {
        int count = 10;
        if (input?.TryGetProperty("count", out var countProp) == true)
        {
            count = countProp.GetInt32();
        }

        var filtered = roomData.OrderByDescending(r => r.AreaSquareMeters).Take(count).ToList();
        var result = new
        {
            query = $"Top {count} largest rooms by area",
            rooms = filtered.Select(r => new { r.RoomNumber, r.RoomName, r.Level, r.AreaSquareMeters, r.DoorCount, r.WindowCount }).ToList()
        };
        return JsonSerializer.Serialize(result);
    }

    private string ExecuteFindSmallestRooms(JsonElement? input, List<RoomData> roomData)
    {
        int count = 10;
        if (input?.TryGetProperty("count", out var countProp) == true)
        {
            count = countProp.GetInt32();
        }

        var filtered = roomData.OrderBy(r => r.AreaSquareMeters).Take(count).ToList();
        var result = new
        {
            query = $"Top {count} smallest rooms by area",
            rooms = filtered.Select(r => new { r.RoomNumber, r.RoomName, r.Level, r.AreaSquareMeters, r.DoorCount, r.WindowCount }).ToList()
        };
        return JsonSerializer.Serialize(result);
    }

    private string ExecuteFindRoomsByAreaRange(JsonElement? input, List<RoomData> roomData)
    {
        double minArea = input?.GetProperty("min_area").GetDouble() ?? 0;
        double maxArea = input?.GetProperty("max_area").GetDouble() ?? 99999;

        var filtered = roomData.Where(r => r.AreaSquareMeters >= minArea && r.AreaSquareMeters <= maxArea)
            .OrderBy(r => r.AreaSquareMeters).ToList();

        var result = new
        {
            query = $"Rooms with area between {minArea} and {maxArea} sqm",
            exact_count = filtered.Count,
            rooms = filtered.Select(r => new { r.RoomNumber, r.RoomName, r.Level, r.AreaSquareMeters }).ToList()
        };
        return JsonSerializer.Serialize(result);
    }

    private string ExecuteCountDoorsByLevel(JsonElement? input, List<RoomData> roomData)
    {
        var levelName = input?.GetProperty("level_name").GetString() ?? "";
        var filtered = roomData.Where(r => r.Level.Contains(levelName, StringComparison.OrdinalIgnoreCase)).ToList();

        var result = new
        {
            query = $"Doors on level containing '{levelName}'",
            total_doors = filtered.Sum(r => r.DoorCount),
            room_count = filtered.Count,
            breakdown = filtered.Select(r => new { r.RoomNumber, r.RoomName, r.DoorCount }).ToList()
        };
        return JsonSerializer.Serialize(result);
    }

    private string ExecuteGetRoomTypesSummary(JsonElement? input, List<RoomData> roomData)
    {
        string? levelFilter = null;
        if (input?.TryGetProperty("level_name", out var levelProp) == true)
        {
            levelFilter = levelProp.GetString();
        }

        var filtered = string.IsNullOrEmpty(levelFilter)
            ? roomData
            : roomData.Where(r => r.Level.Contains(levelFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        var groups = filtered.GroupBy(r => r.RoomName)
            .Select(g => new
            {
                room_type = g.Key,
                count = g.Count(),
                total_area = Math.Round(g.Sum(r => r.AreaSquareMeters), 2),
                rooms = g.Select(r => r.RoomNumber).ToList()
            })
            .OrderByDescending(g => g.count)
            .ToList();

        var result = new
        {
            query = string.IsNullOrEmpty(levelFilter) ? "Room types (all levels)" : $"Room types on level '{levelFilter}'",
            total_room_types = groups.Count,
            breakdown = groups
        };
        return JsonSerializer.Serialize(result);
    }

    private async Task<AnthropicResponse> CallAnthropicApi(List<object> messages)
    {
        var systemPrompt = @"You are an AI assistant analyzing building data from a Revit model.

IMPORTANT INSTRUCTIONS:
1. ALWAYS use the provided tools to get accurate data - never guess or estimate
2. When reporting numbers, use the EXACT values from tool results
3. Format responses clearly with bullet points
4. Include specific room numbers and names when listing rooms
5. Always specify units (sqm for area)

Available tools help you:
- Count rooms by level (exact count)
- Get building summary (totals)
- Find rooms with/without windows
- Find largest/smallest rooms
- Find rooms by area range
- Count doors by level
- Get room types breakdown

After receiving tool results, present the data clearly and verify your counts match the tool's exact_count field.";

        var requestBody = new
        {
            model = "claude-sonnet-4-20250514",
            max_tokens = 2048,
            system = systemPrompt,
            tools = Tools,
            messages = messages
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.PostAsync(_apiUrl, content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"API error ({response.StatusCode}): {responseJson}");
        }

        return JsonSerializer.Deserialize<AnthropicResponse>(responseJson)
            ?? throw new Exception("Failed to parse API response");
    }

}

#region API Response Models

public class AnthropicResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public List<ContentBlock> Content { get; set; } = new();

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}

public class ContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("input")]
    public JsonElement? Input { get; set; }
}

#endregion
