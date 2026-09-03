namespace ARC.Api.Services;

/// <summary>Common abbreviations and alternate names for depot search.</summary>
internal static class DepotQueryAliases
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["wb"] = "West Bengal",
        ["west bengal"] = "West Bengal",
        ["mh"] = "Maharashtra",
        ["maharashtra"] = "Maharashtra",
        ["tn"] = "Tamil Nadu",
        ["tamil nadu"] = "Tamil Nadu",
        ["ka"] = "Karnataka",
        ["karnataka"] = "Karnataka",
        ["ap"] = "Andhra Pradesh",
        ["andhra pradesh"] = "Andhra Pradesh",
        ["ts"] = "Telangana",
        ["tg"] = "Telangana",
        ["telangana"] = "Telangana",
        ["gj"] = "Gujarat",
        ["gujarat"] = "Gujarat",
        ["rj"] = "Rajasthan",
        ["rajasthan"] = "Rajasthan",
        ["up"] = "Uttar Pradesh",
        ["uttar pradesh"] = "Uttar Pradesh",
        ["or"] = "Odisha",
        ["od"] = "Odisha",
        ["odisha"] = "Odisha",
        ["kl"] = "Kerala",
        ["kerala"] = "Kerala",
        ["pb"] = "Punjab",
        ["punjab"] = "Punjab",
        ["hr"] = "Haryana",
        ["haryana"] = "Haryana",
        ["br"] = "Bihar",
        ["bihar"] = "Bihar",
        ["jh"] = "Jharkhand",
        ["jharkhand"] = "Jharkhand",
        ["as"] = "Assam",
        ["assam"] = "Assam",
        ["mp"] = "Madhya Pradesh",
        ["madhya pradesh"] = "Madhya Pradesh",
        ["cg"] = "Chhattisgarh",
        ["ct"] = "Chhattisgarh",
        ["chhattisgarh"] = "Chhattisgarh",
        ["ga"] = "Goa",
        ["goa"] = "Goa",
        ["hp"] = "Himachal Pradesh",
        ["himachal pradesh"] = "Himachal Pradesh",
        ["uk"] = "Uttarakhand",
        ["ut"] = "Uttarakhand",
        ["uttarakhand"] = "Uttarakhand",
        ["dl"] = "Delhi",
        ["delhi"] = "Delhi",
        ["py"] = "Puducherry",
        ["puducherry"] = "Puducherry",
        ["pondicherry"] = "Puducherry",
        ["kolkata"] = "Kolkata",
        ["calcutta"] = "Kolkata",
        ["howrah"] = "Howrah",
    };

    public static string? Resolve(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var key = text.Trim();
        return Aliases.TryGetValue(key, out var expanded) ? expanded : text;
    }
}
