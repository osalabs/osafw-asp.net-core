using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace osafw;

public sealed class AssistantNavigationCatalog
{
    public const string TemplateBaseDir = "/assistant/prompts";
    public const string CatalogTemplate = "navigation_catalog.json";

    public int version { get; set; }
    public List<AssistantNavigationController> controllers { get; set; } = [];

    public static AssistantNavigationCatalog Load(FW fw)
    {
        string json = fw.parsePage(TemplateBaseDir, CatalogTemplate, []);
        return Parse(json);
    }

    public static AssistantNavigationCatalog Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new AssistantNavigationCatalog();

        try
        {
            return JsonSerializer.Deserialize<AssistantNavigationCatalog>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AssistantNavigationCatalog();
        }
        catch (Exception ex)
        {
            throw new UserException("Assistant navigation catalog is invalid: " + ex.Message);
        }
    }

    public static bool IsAppLocalUrl(string url)
    {
        return !string.IsNullOrWhiteSpace(url)
            && url.StartsWith("/", StringComparison.Ordinal)
            && !url.StartsWith("//", StringComparison.Ordinal)
            && !url.Contains('\\')
            && !url.Any(char.IsControl);
    }

    public FwList find(FW fw, string query, string action = "", string filtersJson = "", string prefillJson = "", int id = 0, int k = 5)
    {
        string normalizedQuery = normalizeText(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return [];

        string requestedAction = normalizeAction(action);
        var ranked = controllers
            .Where(controller => controller != null && controller.isAllowed(fw.userAccessLevel) && IsAppLocalUrl(controller.url))
            .Select(controller => buildCandidate(controller, normalizedQuery, requestedAction, filtersJson, prefillJson, id))
            .Where(static candidate => candidate != null && candidate.score > 0 && !string.IsNullOrWhiteSpace(candidate.url))
            .OrderByDescending(static candidate => candidate!.score)
            .ThenBy(static candidate => candidate!.label, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(k, 1, 10))
            .ToList();

        var output = new FwList(ranked.Count);
        foreach (var candidate in ranked)
        {
            output.Add(new FwDict
            {
                ["label"] = candidate!.label,
                ["url"] = candidate.url,
                ["action"] = candidate.action,
                ["description"] = candidate.description,
                ["controller_url"] = candidate.controllerUrl,
                ["score"] = candidate.score,
                ["warnings"] = candidate.warnings
            });
        }

        return output;
    }

    private static AssistantNavigationCandidate? buildCandidate(AssistantNavigationController controller, string normalizedQuery, string requestedAction, string filtersJson, string prefillJson, int id)
    {
        double score = scoreController(controller, normalizedQuery);
        if (score <= 0)
            return null;

        string effectiveAction = requestedAction;
        if (string.IsNullOrWhiteSpace(effectiveAction))
            effectiveAction = inferAction(normalizedQuery, controller);
        var warnings = new List<string>();
        if (!controller.allowsAction(effectiveAction))
        {
            string fallbackAction = fallbackUnsupportedAction(effectiveAction, controller);
            if (string.IsNullOrWhiteSpace(fallbackAction))
                return null;

            warnings.Add("Requested action " + effectiveAction + " is not declared for this screen; opened " + fallbackAction + " instead.");
            effectiveAction = fallbackAction;
        }

        string candidateAction = effectiveAction;
        string url;
        if (effectiveAction == "new")
        {
            var prefill = parseParameters(prefillJson, controller.prefill_fields, warnings, "prefill");
            url = buildNewUrl(controller.url, prefill);
        }
        else if (effectiveAction == "view" || effectiveAction == "edit")
        {
            if (id <= 0)
            {
                if (!controller.allowsAction("list"))
                    return null;

                candidateAction = "list";
                warnings.Add("Record id is required for " + effectiveAction + "; opened the list instead.");
                var filters = parseParameters(filtersJson, controller.list_filters, warnings, "filter");
                url = buildListUrl(controller.url, filters);
            }
            else
            {
                url = buildRecordUrl(controller.url, id, effectiveAction);
            }
        }
        else
        {
            var filters = parseParameters(filtersJson, controller.list_filters, warnings, "filter");
            url = buildListUrl(controller.url, filters);
        }

        return new AssistantNavigationCandidate(
            controller.label,
            url,
            candidateAction,
            controller.description,
            controller.url,
            score + actionBoost(candidateAction, normalizedQuery),
            warnings
        );
    }

    private static double scoreController(AssistantNavigationController controller, string normalizedQuery)
    {
        var terms = splitTerms(normalizedQuery);
        if (terms.Count == 0)
            return 0;

        double score = 0;
        string label = normalizeText(controller.label);
        string description = normalizeText(controller.description);
        string route = normalizeText(controller.url + " " + controller.controller);
        var keywords = controller.keywords.Select(normalizeText).Where(static value => value.Length > 0).ToList();

        if (label.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            score += 20;
        if (description.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            score += 8;
        if (route.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            score += 6;
        if (keywords.Any(keyword => keyword.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            score += 18;

        foreach (string term in terms)
        {
            if (keywords.Any(keyword => keyword == term))
                score += 10;
            else if (keywords.Any(keyword => keyword.Contains(term, StringComparison.OrdinalIgnoreCase)))
                score += 6;

            if (label.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 8;
            if (route.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 4;
            if (description.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 3;
        }

        return score;
    }

    private static double actionBoost(string action, string normalizedQuery)
    {
        if (action == "new" && containsAny(normalizedQuery, ["add", "create", "new"]))
            return 8;
        if (action == "edit" && containsAny(normalizedQuery, ["edit", "update", "change"]))
            return 8;
        if (action == "view" && containsAny(normalizedQuery, ["view", "open", "show"]))
            return 6;
        if (action == "list" && containsAny(normalizedQuery, ["find", "list", "search", "show"]))
            return 4;
        return 0;
    }

    private static string inferAction(string normalizedQuery, AssistantNavigationController controller)
    {
        if (containsAny(normalizedQuery, ["add", "create", "new"]) && controller.allowsAction("new"))
            return "new";
        if (containsAny(normalizedQuery, ["edit", "update", "change"]) && controller.allowsAction("edit"))
            return "edit";
        if (containsAny(normalizedQuery, ["view", "open"]) && controller.allowsAction("view"))
            return "view";
        if (controller.allowsAction("list"))
            return "list";
        if (controller.allowsAction("new"))
            return "new";
        return controller.actions.FirstOrDefault() ?? string.Empty;
    }

    private static string fallbackUnsupportedAction(string action, AssistantNavigationController controller)
    {
        if ((action == "edit" || action == "view") && controller.allowsAction("list"))
            return "list";
        if ((action == "edit" || action == "view" || action == "list") && controller.allowsAction("new"))
            return "new";
        if (action == "new" && controller.allowsAction("list"))
            return "list";
        return string.Empty;
    }

    private static string normalizeAction(string action)
    {
        string value = normalizeText(action);
        return value switch
        {
            "add" or "create" or "new" => "new",
            "edit" or "update" or "change" => "edit",
            "view" or "open" or "show" => "view",
            "list" or "find" or "search" => "list",
            _ => value
        };
    }

    private static Dictionary<string, string> parseParameters(string json, List<AssistantNavigationField> allowedFields, List<string> warnings, string kind)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        Dictionary<string, AssistantNavigationField> allowed = allowedFields
            .Where(static field => !string.IsNullOrWhiteSpace(field.name))
            .ToDictionary(static field => field.name, static field => field, StringComparer.OrdinalIgnoreCase);
        if (allowed.Count == 0)
        {
            warnings.Add("No " + kind + " fields are declared for this screen.");
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                warnings.Add(kind + " JSON must be an object.");
                return [];
            }

            Dictionary<string, string> result = [];
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!allowed.TryGetValue(property.Name, out var field))
                {
                    warnings.Add("Unsupported " + kind + " field: " + property.Name);
                    continue;
                }

                string value = jsonValueToString(property.Value);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (field.options.Count > 0 && !field.options.ContainsKey(value))
                {
                    warnings.Add("Unsupported " + kind + " value for " + field.name + ": " + value);
                    continue;
                }

                result[field.name] = value;
            }

            return result;
        }
        catch
        {
            warnings.Add(kind + " JSON is invalid.");
            return [];
        }
    }

    private static string buildListUrl(string baseUrl, Dictionary<string, string> filters)
    {
        if (filters.Count == 0)
            return baseUrl;

        var query = new List<string> { "dofilter=1" };
        query.AddRange(filters.Select(static item => encodeParam("f[" + item.Key + "]", item.Value)));
        return baseUrl + "?" + string.Join("&", query);
    }

    private static string buildNewUrl(string baseUrl, Dictionary<string, string> prefill)
    {
        string url = baseUrl.TrimEnd('/') + "/new";
        if (prefill.Count == 0)
            return url;

        return url + "?" + string.Join("&", prefill.Select(static item => encodeParam("item[" + item.Key + "]", item.Value)));
    }

    private static string buildRecordUrl(string baseUrl, int id, string action)
    {
        string url = baseUrl.TrimEnd('/') + "/" + id;
        return action == "edit" ? url + "/edit" : url;
    }

    private static string encodeParam(string name, string value)
    {
        return Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(value);
    }

    private static string jsonValueToString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => string.Join(",", value.EnumerateArray().Select(jsonValueToString).Where(static item => !string.IsNullOrWhiteSpace(item))),
            _ => string.Empty
        };
    }

    private static bool containsAny(string value, IEnumerable<string> terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> splitTerms(string value)
    {
        string normalized = normalizeText(value);
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(static term => term.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string normalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Select(static ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        return string.Join(" ", new string(chars.ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record AssistantNavigationCandidate(string label, string url, string action, string description, string controllerUrl, double score, List<string> warnings);
}

public sealed class AssistantNavigationController
{
    public string url { get; set; } = string.Empty;
    public string controller { get; set; } = string.Empty;
    public string label { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public List<string> keywords { get; set; } = [];
    public int min_access_level { get; set; }
    public List<string> actions { get; set; } = [];
    public List<AssistantNavigationField> list_filters { get; set; } = [];
    public List<AssistantNavigationField> prefill_fields { get; set; } = [];

    public bool isAllowed(int userAccessLevel)
    {
        return userAccessLevel >= min_access_level;
    }

    public bool allowsAction(string action)
    {
        return !string.IsNullOrWhiteSpace(action)
            && actions.Contains(action, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class AssistantNavigationField
{
    public string name { get; set; } = string.Empty;
    public string label { get; set; } = string.Empty;
    public string type { get; set; } = "text";
    public Dictionary<string, string> options { get; set; } = [];
}
