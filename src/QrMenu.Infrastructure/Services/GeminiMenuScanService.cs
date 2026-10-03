using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.MenuScan;

namespace QrMenu.Infrastructure.Services;

public class GeminiMenuScanService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiMenuScanService> logger) : IMenuScanService
{
    private const string Model = "gemini-flash-lite-latest";

    private const string PromptText = """
        You are reading one or more photos of a restaurant's physical menu. Extract every dish into a JSON array.
        Each element must have exactly these fields:
        - "category": the menu section this dish appears under (e.g. "Starters", "Main Course", "Beverages"). If no section heading is visible, infer a reasonable one from the dish itself.
        - "name": the dish name, cleaned up (fix obvious OCR typos, use title case).
        - "price": the numeric price only (no currency symbol, no commas). Use null if no price is visible for this dish.
        - "isVeg": true if the dish is vegetarian, false if it contains meat, egg, or fish. Use any veg/non-veg symbol on the menu if present, otherwise infer from the dish name.
        - "description": a short description if the menu shows one, otherwise null.
        If multiple photos are provided, treat them as pages of the same menu and return one combined list without duplicate section headers.
        Return ONLY the JSON array. Do not include markdown fences, explanations, or any other text.
        """;

    public async Task<MenuScanResultDto> ScanAsync(List<MenuScanImage> images, CancellationToken ct = default)
    {
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ConflictException(
                "Menu scanning isn't set up yet. Add a Gemini API key in the server configuration to enable this feature.");
        }

        var parts = new List<object> { new { text = PromptText } };
        foreach (var image in images)
        {
            using var ms = new MemoryStream();
            await image.Content.CopyToAsync(ms, ct);
            parts.Add(new { inline_data = new { mime_type = image.ContentType, data = Convert.ToBase64String(ms.ToArray()) } });
        }

        var requestBody = new
        {
            contents = new[] { new { parts } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={apiKey}")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ConflictException($"Could not reach the menu scanning service: {ex.Message}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Gemini menu scan request failed ({StatusCode}): {Body}", (int)response.StatusCode, responseBody);
            throw new ConflictException($"Menu scanning service returned an error ({(int)response.StatusCode}). Please try again.");
        }

        string? text;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
        }
        catch (Exception)
        {
            throw new ConflictException("Could not read a response from the menu scanning service. Please try again.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ConflictException("The menu scanning service did not return any items. Try a clearer photo.");
        }

        List<MenuScanItemDto>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<MenuScanItemDto>>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            throw new ConflictException("Could not understand the menu in that photo. Try a clearer, well-lit photo.");
        }

        return new MenuScanResultDto(items ?? []);
    }
}
