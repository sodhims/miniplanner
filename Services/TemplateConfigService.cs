using Microsoft.AspNetCore.Components;
using System.Text.Json;

namespace dfd2wasm.Services;

/// <summary>
/// Service for managing template configuration.
/// Templates can be enabled/disabled via:
/// 1. URL query parameters: ?templates=circuit,flowchart,sts
/// 2. Preset names: ?preset=engineering
/// 3. JSON config file: wwwroot/templates.json
/// </summary>
public class TemplateConfigService
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;

    private HashSet<string>? _enabledTemplates;
    private TemplateConfig? _config;
    private bool _initialized = false;

    public TemplateConfigService(HttpClient httpClient, NavigationManager navigationManager)
    {
        _httpClient = httpClient;
        _navigationManager = navigationManager;
    }

    /// <summary>
    /// Initialize the service by loading config and parsing URL parameters
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            // Load the JSON config file
            _config = await LoadConfigAsync();

            // Parse URL parameters to determine enabled templates
            _enabledTemplates = ParseUrlParameters();

            _initialized = true;
            Console.WriteLine($"TemplateConfigService initialized. Enabled templates: {string.Join(", ", _enabledTemplates)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing TemplateConfigService: {ex.Message}");
            // Default to all templates if initialization fails
            _enabledTemplates = new HashSet<string> { "circuit", "flowchart", "icd", "network", "bpmn", "sts", "project" };
            _initialized = true;
        }
    }

    /// <summary>
    /// Check if a template is enabled
    /// </summary>
    public bool IsTemplateEnabled(string templateId)
    {
        if (!_initialized || _enabledTemplates == null)
            return true; // Default to enabled if not initialized

        return _enabledTemplates.Contains(templateId);
    }

    /// <summary>
    /// Get all enabled template IDs
    /// </summary>
    public IEnumerable<string> GetEnabledTemplateIds()
    {
        if (!_initialized || _enabledTemplates == null)
            return new[] { "circuit", "flowchart", "icd", "network", "bpmn", "sts", "project" };

        return _enabledTemplates;
    }

    /// <summary>
    /// Get URL for a specific preset
    /// </summary>
    public string GetPresetUrl(string presetName)
    {
        var baseUri = _navigationManager.BaseUri;
        return $"{baseUri}?preset={presetName}";
    }

    /// <summary>
    /// Get URL for specific templates
    /// </summary>
    public string GetTemplatesUrl(params string[] templateIds)
    {
        var baseUri = _navigationManager.BaseUri;
        return $"{baseUri}?templates={string.Join(",", templateIds)}";
    }

    /// <summary>
    /// Get available presets from config
    /// </summary>
    public Dictionary<string, string[]>? GetPresets()
    {
        return _config?.Presets;
    }

    /// <summary>
    /// Get the default mode/template to activate on startup
    /// </summary>
    public string? GetDefaultMode()
    {
        return _config?.DefaultMode;
    }

    private async Task<TemplateConfig?> LoadConfigAsync()
    {
        try
        {
            // Add cache-busting query parameter to ensure fresh load
            var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var response = await _httpClient.GetAsync($"templates.json?_={cacheBuster}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"LoadConfigAsync: Loaded JSON ({json.Length} chars)");
                Console.WriteLine($"LoadConfigAsync: JSON content: {json}");
                var config = JsonSerializer.Deserialize<TemplateConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                Console.WriteLine($"LoadConfigAsync: Parsed defaultMode = '{config?.DefaultMode ?? "(null)"}'");
                return config;
            }
            else
            {
                Console.WriteLine($"LoadConfigAsync: HTTP {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load templates.json: {ex.Message}");
        }
        return null;
    }

    private HashSet<string> ParseUrlParameters()
    {
        var uri = new Uri(_navigationManager.Uri);
        var queryParams = ParseQueryString(uri.Query);

        // Check for ?templates=circuit,flowchart,sts
        if (queryParams.TryGetValue("templates", out var templatesValue))
        {
            var templateList = templatesValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLowerInvariant())
                .ToHashSet();

            if (templateList.Count > 0)
            {
                Console.WriteLine($"URL templates parameter: {string.Join(", ", templateList)}");
                return templateList;
            }
        }

        // Check for ?preset=engineering
        if (queryParams.TryGetValue("preset", out var presetValue))
        {
            var presetName = presetValue.Trim().ToLowerInvariant();
            if (_config?.Presets != null && _config.Presets.TryGetValue(presetName, out var presetTemplates))
            {
                Console.WriteLine($"Using preset '{presetName}': {string.Join(", ", presetTemplates)}");
                return presetTemplates.ToHashSet();
            }
        }

        // Fall back to config file settings
        if (_config?.Templates != null)
        {
            var enabledFromConfig = _config.Templates
                .Where(kvp => kvp.Value.Enabled)
                .Select(kvp => kvp.Key)
                .ToHashSet();

            if (enabledFromConfig.Count > 0)
            {
                Console.WriteLine($"Using config file templates: {string.Join(", ", enabledFromConfig)}");
                return enabledFromConfig;
            }
        }

        // Default to all templates
        return new HashSet<string> { "circuit", "flowchart", "icd", "network", "bpmn", "sts" };
    }

    /// <summary>
    /// Simple query string parser (avoids WebUtilities dependency)
    /// </summary>
    private static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(queryString))
            return result;

        // Remove leading '?'
        if (queryString.StartsWith('?'))
            queryString = queryString[1..];

        foreach (var pair in queryString.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = Uri.UnescapeDataString(parts[0]);
                var value = Uri.UnescapeDataString(parts[1]);
                result[key] = value;
            }
            else if (parts.Length == 1)
            {
                var key = Uri.UnescapeDataString(parts[0]);
                result[key] = "";
            }
        }

        return result;
    }
}

/// <summary>
/// Model for templates.json configuration file
/// </summary>
public class TemplateConfig
{
    public string? Description { get; set; }
    /// <summary>
    /// The default mode/template to activate on startup (e.g., "project", "flowchart")
    /// </summary>
    public string? DefaultMode { get; set; }
    public Dictionary<string, TemplateSettings>? Templates { get; set; }
    public Dictionary<string, string[]>? Presets { get; set; }
}

public class TemplateSettings
{
    public bool Enabled { get; set; } = true;
    public string? DisplayName { get; set; }
}
