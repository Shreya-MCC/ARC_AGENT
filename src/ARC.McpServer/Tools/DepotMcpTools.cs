using System.ComponentModel;
using ARC.Tools.Depot;
using ModelContextProtocol.Server;

namespace ARC.McpServer.Tools;

[McpServerToolType]
public sealed class DepotMcpTools
{
    private readonly DepotMasterTool _tool;

    public DepotMcpTools(DepotMasterTool tool) => _tool = tool;

    [McpServerTool, Description("Search depot master data by depot code, depot name (partial match), or region.")]
    public async Task<string> SearchDepotMaster(
        [Description("Exact depot code, e.g. 006")] string? depotCode = null,
        [Description("Partial depot name, e.g. Howrah")] string? depotName = null,
        [Description("Region code, e.g. E1-WB")] string? regnNew = null,
        [Description("Fuzzy search across depot name, state, and region")] string? searchText = null,
        [Description("Optional correlation id")] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _tool.SearchAsync(
            new SearchDepotMasterRequest(depotCode, depotName, regnNew, searchText, correlationId),
            cancellationToken);

        return McpJson.Serialize(result);
    }
}
