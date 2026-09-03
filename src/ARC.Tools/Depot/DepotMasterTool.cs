using Microsoft.Extensions.Logging;
using ARC.Data.Exceptions;
using ARC.Data.Sql;
using ARC.Tools.Exceptions;

namespace ARC.Tools.Depot;

public sealed record SearchDepotMasterRequest(
    string? DepotCode,
    string? DepotName,
    string? RegnNew,
    string? SearchText,
    string? CorrelationId);

public sealed record SearchDepotMasterResult(
    IReadOnlyList<DepotMaster> Depots,
    int Count);

/// <summary>Search depot master by code, name (partial), or region via dbo.usp_GetDepotMaster.</summary>
public sealed class DepotMasterTool
{
    public const string Name = "SearchDepotMaster";

    private readonly IDepotMasterRepository _depots;
    private readonly ILogger<DepotMasterTool> _logger;

    public DepotMasterTool(IDepotMasterRepository depots, ILogger<DepotMasterTool> logger)
    {
        _depots = depots;
        _logger = logger;
    }

    public async Task<SearchDepotMasterResult> SearchAsync(
        SearchDepotMasterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DepotCode)
            && string.IsNullOrWhiteSpace(request.DepotName)
            && string.IsNullOrWhiteSpace(request.RegnNew)
            && string.IsNullOrWhiteSpace(request.SearchText))
        {
            throw new ToolException(Name, "Provide at least one filter: depotCode, depotName, regnNew, or searchText.");
        }

        var started = DateTimeOffset.UtcNow;
        try
        {
            var depots = await _depots.SearchAsync(
                request.DepotCode,
                request.DepotName,
                request.RegnNew,
                request.SearchText,
                cancellationToken);

            _logger.LogInformation(
                "Tool {Tool} depotCode {DepotCode} depotName {DepotName} regnNew {RegnNew} correlation {CorrelationId} count {Count} durationMs {DurationMs}",
                Name,
                request.DepotCode,
                request.DepotName,
                request.RegnNew,
                request.CorrelationId,
                depots.Count,
                (DateTimeOffset.UtcNow - started).TotalMilliseconds);

            return new SearchDepotMasterResult(depots, depots.Count);
        }
        catch (DataAccessException ex)
        {
            throw new ToolException(Name, "Depot master search failed.", ex);
        }
    }
}
