using System.Net;
using System.Text;
using ARC.Data.Sql;

namespace ARC.Api.Services;

internal enum DepotReplyFormat
{
    List,
    Table,
    Grid,
    Csv
}

internal static class DepotResponseFormatter
{
    private const int DisplayLimit = 50;

    public static (string Reply, string ReplyFormat) Format(
        IReadOnlyList<DepotMaster> depots,
        DepotReplyFormat format)
    {
        var shown = depots.Take(DisplayLimit).ToList();
        var truncated = depots.Count > DisplayLimit;

        return format switch
        {
            DepotReplyFormat.Table => (FormatTable(shown, depots.Count, truncated), "html"),
            DepotReplyFormat.Grid => (FormatGrid(shown, depots.Count, truncated), "html"),
            DepotReplyFormat.Csv => (FormatCsv(shown, depots.Count, truncated), "text"),
            _ => (FormatList(shown, depots.Count, truncated), "text")
        };
    }

    private static string FormatList(IReadOnlyList<DepotMaster> depots, int total, bool truncated)
    {
        var text = new StringBuilder();
        text.AppendLine($"Found **{total}** depot(s):\n");

        foreach (var depot in depots)
        {
            text.AppendLine($"• **{depot.DepotCode}** — {depot.DepotName}");
            text.AppendLine($"  Region: {depot.RegnNew ?? "n/a"} · State: {depot.DepotState ?? "n/a"}");
        }

        if (truncated)
            text.AppendLine($"\n_Showing first {DisplayLimit} of {total} results._");

        return text.ToString().Trim();
    }

    private static string FormatTable(IReadOnlyList<DepotMaster> depots, int total, bool truncated)
    {
        var sb = new StringBuilder();
        sb.Append($"<p class=\"result-summary\"><strong>Found {total} depot(s)</strong>");
        if (truncated)
            sb.Append($" — showing first {DisplayLimit}");
        sb.Append("</p>");
        sb.Append("<div class=\"depot-table-wrap\"><table class=\"depot-table\"><thead><tr>");
        sb.Append("<th>Code</th><th>Depot Name</th><th>Region</th><th>State</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var depot in depots)
        {
            sb.Append("<tr>");
            sb.Append($"<td>{E(depot.DepotCode)}</td>");
            sb.Append($"<td>{E(depot.DepotName)}</td>");
            sb.Append($"<td>{E(depot.RegnNew ?? "n/a")}</td>");
            sb.Append($"<td>{E(depot.DepotState ?? "n/a")}</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table></div>");
        return sb.ToString();
    }

    private static string FormatGrid(IReadOnlyList<DepotMaster> depots, int total, bool truncated)
    {
        var sb = new StringBuilder();
        sb.Append($"<p class=\"result-summary\"><strong>Found {total} depot(s)</strong>");
        if (truncated)
            sb.Append($" — showing first {DisplayLimit}");
        sb.Append("</p><div class=\"depot-grid\">");

        foreach (var depot in depots)
        {
            sb.Append("<article class=\"depot-card\">");
            sb.Append($"<div class=\"depot-card-code\">{E(depot.DepotCode)}</div>");
            sb.Append($"<div class=\"depot-card-name\">{E(depot.DepotName)}</div>");
            sb.Append($"<div class=\"depot-card-meta\">Region: {E(depot.RegnNew ?? "n/a")}</div>");
            sb.Append($"<div class=\"depot-card-meta\">State: {E(depot.DepotState ?? "n/a")}</div>");
            sb.Append("</article>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string FormatCsv(IReadOnlyList<DepotMaster> depots, int total, bool truncated)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Found {total} depot(s):");
        if (truncated)
            sb.AppendLine($"Showing first {DisplayLimit} of {total}");
        sb.AppendLine();
        sb.AppendLine("Code,Depot Name,Region,State");

        foreach (var depot in depots)
        {
            sb.Append(Csv(depot.DepotCode)).Append(',');
            sb.Append(Csv(depot.DepotName)).Append(',');
            sb.Append(Csv(depot.RegnNew ?? "n/a")).Append(',');
            sb.AppendLine(Csv(depot.DepotState ?? "n/a"));
        }

        return sb.ToString().Trim();
    }

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
