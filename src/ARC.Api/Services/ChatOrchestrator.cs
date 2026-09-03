using System.Text;

using System.Text.RegularExpressions;

using ARC.Api.Auth;

using ARC.Api.DTOs;

using ARC.Data.Sql;

using ARC.Tools.Depot;

using ARC.Tools.Exceptions;



namespace ARC.Api.Services;



/// <summary>Depot-only chat — answers depot master queries with session-scoped history.</summary>
public sealed class ChatOrchestrator
{
    private static readonly Regex FormatPattern = new(

        @"\b(?:in|as|show|give|display|present|format)\s+(?:a\s+)?(table|tabular|grid|csv|list)(?:\s+format)?\b"

        + @"|\b(table|tabular|grid|csv|list)\s+(?:format|view)\b",

        RegexOptions.IgnoreCase | RegexOptions.Compiled);



    private readonly DepotMasterTool _depots;
    private readonly ChatSessionStore _sessions;

    public ChatOrchestrator(DepotMasterTool depots, ChatSessionStore sessions)
    {
        _depots = depots;
        _sessions = sessions;
    }

    public async Task<ChatMessageResponse> HandleAsync(
        ChatMessageRequest request,
        ArcActor actor,
        CancellationToken cancellationToken)
    {
        var sessionId = await _sessions.ResolveSessionIdAsync(request.SessionId, actor, cancellationToken);
        var message = request.Message.Trim();

        if (string.IsNullOrWhiteSpace(message))
            return await FinalizeAsync(sessionId, message, Reply("Please type a message.", "ARC Assistant"), actor, cancellationToken);

        if (IsHelp(message))
            return await FinalizeAsync(sessionId, message, Reply(BuildHelpText(), "ARC Assistant"), actor, cancellationToken);

        var (queryMessage, format) = ExtractFormat(message);
        var filters = ParseDepotQuery(queryMessage);

        if (filters.Code is null && filters.Name is null && filters.Region is null && filters.SearchText is null)
        {
            return await FinalizeAsync(
                sessionId,
                message,
                Reply(
                    "I can look up **depot master** data. Try:\n"
                    + "• `006` — depot by code\n"
                    + "• `Howrah` or `WB` — depot by name/state\n"
                    + "• `E1-WB` or `C1` — depots in a region\n"
                    + "• `WB depots in table format` — tabular results\n\n"
                    + "Type **help** for more examples.",
                    "ARC Assistant"),
                actor,
                cancellationToken);
        }

        try
        {
            var result = await _depots.SearchAsync(
                new SearchDepotMasterRequest(filters.Code, filters.Name, filters.Region, filters.SearchText, null),
                cancellationToken);

            if (result.Count == 0)
            {
                return await FinalizeAsync(
                    sessionId,
                    message,
                    Reply(
                        "No depots matched your search. Try a different code, name, or region.",
                        DepotMasterTool.Name,
                        data: result),
                    actor,
                    cancellationToken);
            }

            var (reply, replyFormat) = DepotResponseFormatter.Format(result.Depots, format);
            return await FinalizeAsync(
                sessionId,
                message,
                Reply(reply, DepotMasterTool.Name, replyFormat, result),
                actor,
                cancellationToken);
        }
        catch (ToolException ex)
        {
            return await FinalizeAsync(sessionId, message, Reply(ex.Message, DepotMasterTool.Name), actor, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FinalizeAsync(sessionId, message, Reply($"Something went wrong: {ex.Message}", "ARC Assistant"), actor, cancellationToken);
        }
    }

    private async Task<ChatMessageResponse> FinalizeAsync(
        string sessionId,
        string userMessage,
        ChatMessageResponse response,
        ArcActor actor,
        CancellationToken cancellationToken)
    {
        var saved = false;
        string? historyError = null;
        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            (saved, historyError) = await _sessions.PersistTurnAsync(
                sessionId,
                userMessage,
                response,
                actor,
                cancellationToken);
        }

        return response with
        {
            SessionId = sessionId,
            HistorySaved = saved,
            HistoryError = historyError
        };
    }



    private sealed record DepotSearchFilters(

        string? Code,

        string? Name,

        string? Region,

        string? SearchText);



    private static (string Query, DepotReplyFormat Format) ExtractFormat(string message)

    {

        DepotReplyFormat format = DepotReplyFormat.List;

        var query = message;



        var match = FormatPattern.Match(message);

        if (match.Success)

        {

            var token = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

            format = token.ToLowerInvariant() switch

            {

                "table" or "tabular" => DepotReplyFormat.Table,

                "grid" => DepotReplyFormat.Grid,

                "csv" => DepotReplyFormat.Csv,

                _ => DepotReplyFormat.List

            };

            query = FormatPattern.Replace(message, " ").Trim();

        }



        return (query, format);

    }



    private static DepotSearchFilters ParseDepotQuery(string message)

    {

        var code = ExtractDepotCode(message);

        if (code is not null)

            return new DepotSearchFilters(code, null, null, null);



        var region = ExtractRegion(message);

        if (region is not null)

            return new DepotSearchFilters(null, null, region, null);



        if (ContainsRegionOrStateIntent(message))

            return new DepotSearchFilters(null, null, null, ResolveSearchText(ExtractSearchText(message)));



        var name = ExtractDepotName(message);

        if (name is not null)

            return new DepotSearchFilters(null, ResolveSearchText(name), null, null);



        return new DepotSearchFilters(null, null, null, ResolveSearchText(ExtractSearchText(message)));

    }



    private static string? ResolveSearchText(string? text) => DepotQueryAliases.Resolve(text);



    private static bool ContainsRegionOrStateIntent(string message)

        => Regex.IsMatch(message, @"\b(region|state)\b", RegexOptions.IgnoreCase);



    private static string? ExtractSearchText(string message)

    {

        var text = message.Trim().TrimEnd('?', '.', '!');



        var beforeRegion = Regex.Match(text, @"^(.+?)\s+region(?:\s+depot|\s+names?)?\s*$", RegexOptions.IgnoreCase);

        if (beforeRegion.Success)

            text = beforeRegion.Groups[1].Value.Trim();



        text = Regex.Replace(

            text,

            @"\b(what|is|are|the|all|list|show|find|search|tell|me|give|names?|depots?|under|in|from|of|for|please|about)\b",

            " ",

            RegexOptions.IgnoreCase);

        text = Regex.Replace(text, @"\b(region|state)\b", " ", RegexOptions.IgnoreCase);

        text = Regex.Replace(text, @"\s+", " ").Trim();



        return string.IsNullOrWhiteSpace(text) ? null : text;

    }



    private static string? ExtractRegion(string message)

    {

        var full = Regex.Match(message, @"\b([ENCW]\d-[A-Z]{2}(?:-[A-Z]+)?)\b", RegexOptions.IgnoreCase);

        if (full.Success)

            return full.Groups[1].Value.ToUpperInvariant();



        string[] partialPatterns =

        [

            @"(?:under|in|from)\s+([ENCW]\d)\b",

            @"\b([ENCW]\d)\s+region\b",

            @"region\s+([ENCW]\d)\b",

            @"\b([ENCW]\d)\s+depot",

            @"^([ENCW]\d)\b"

        ];



        foreach (var pattern in partialPatterns)

        {

            var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);

            if (match.Success)

                return match.Groups[1].Value.ToUpperInvariant();

        }



        var afterRegion = Regex.Match(message, @"region\s+([ENCW]\d(?:-[A-Z0-9-]+)?)\b", RegexOptions.IgnoreCase);

        if (afterRegion.Success)

            return afterRegion.Groups[1].Value.ToUpperInvariant();



        var underFull = Regex.Match(message, @"(?:under|in)\s+([ENCW]\d-[A-Z0-9-]+)", RegexOptions.IgnoreCase);

        if (underFull.Success)

            return underFull.Groups[1].Value.ToUpperInvariant();



        return null;

    }



    private static string? ExtractDepotCode(string message)

    {

        string[] patterns =

        [

            @"(?:depot|code)\s+(?:name\s+)?(?:of|for)\s+(\d{2,4})\b",

            @"(?:depot|code)\s+(\d{2,4})\b",

            @"\b(?:of|for)\s+(\d{2,4})\b",

            @"^(\d{2,4})\b",

            @"\b(\d{2,4})\b"

        ];



        foreach (var pattern in patterns)

        {

            var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);

            if (match.Success)

                return match.Groups[1].Value;

        }



        return null;

    }



    private static string? ExtractDepotName(string message)

    {

        string[] patterns =

        [

            @"depot\s+name\s+(?:of|for)?\s*([A-Za-z][\w\s-]+?)[\?\.]?\s*$",

            @"name\s+(?:of\s+)?(?:the\s+)?depot\s+([A-Za-z][\w\s-]+?)[\?\.]?\s*$",

            @"(?:find|search|show|which)\s+(.+?)\s+depot\b",

            @"^([A-Za-z][\w\s-]+?)\s+depot\b",

            @"depot\s+([A-Za-z][\w\s-]+?)[\?\.]?\s*$"

        ];



        foreach (var pattern in patterns)

        {

            var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);

            if (!match.Success)

                continue;



            var name = match.Groups[1].Value.Trim();

            if (!Regex.IsMatch(name, @"^\d+$")

                && !name.StartsWith("of ", StringComparison.OrdinalIgnoreCase)

                && !name.StartsWith("for ", StringComparison.OrdinalIgnoreCase))

            {

                return name;

            }

        }



        return null;

    }



    private static bool IsHelp(string message)

        => message.Equals("help", StringComparison.OrdinalIgnoreCase)

           || message.Equals("?", StringComparison.OrdinalIgnoreCase)

           || message.StartsWith("/help", StringComparison.OrdinalIgnoreCase);



    private static string BuildHelpText() =>

        """

        *ARC Assistant* — ask about depots directly. Each chat keeps its own session history.



        Examples:

        • `006` or `what is the depot name of 006?`

        • `Howrah` or `WB` / `West Bengal`

        • `E1-WB` or `what depots are under C1?`

        • `WB depots in table format` — tabular grid

        • `list west bengal as grid` — card layout

        • `E1-WB as csv` — comma-separated export

        """;



    private static ChatMessageResponse Reply(

        string reply,

        string agent,

        string replyFormat = "text",

        object? data = null)

        => new()
        {
            SessionId = "",
            Reply = reply,

            Agent = agent,

            Timestamp = DateTimeOffset.UtcNow,

            ReplyFormat = replyFormat,

            Data = data

        };

}


