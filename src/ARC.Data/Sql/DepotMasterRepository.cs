using Dapper;

namespace ARC.Data.Sql;

public sealed record DepotMaster(
    string DepotCode,
    string DepotName,
    string? RegnNew,
    string? DepotState);

public interface IDepotMasterRepository
{
    Task<IReadOnlyList<DepotMaster>> SearchAsync(
        string? depotCode,
        string? depotName,
        string? regnNew,
        string? searchText,
        CancellationToken cancellationToken);
}

public sealed class DepotMasterRepository : IDepotMasterRepository
{
    private readonly ISqlConnectionFactory _connections;

    public DepotMasterRepository(ISqlConnectionFactory connections) => _connections = connections;

    public async Task<IReadOnlyList<DepotMaster>> SearchAsync(
        string? depotCode,
        string? depotName,
        string? regnNew,
        string? searchText,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT d.depot_code, d.depot_name, d.regn_new, d.depot_state
            FROM dbo.depot_mstr AS d WITH(NOLOCK)
            WHERE (@depot_code IS NULL OR d.depot_code = @depot_code)
              AND (@depot_name IS NULL OR d.depot_name LIKE N'%' + @depot_name + N'%')
              AND (@regn_new IS NULL OR d.regn_new LIKE @regn_new + N'%')
              AND (@search_text IS NULL OR d.depot_name LIKE N'%' + @search_text + N'%'
                   OR d.depot_state LIKE N'%' + @search_text + N'%'
                   OR d.regn_new LIKE N'%' + @search_text + N'%')
            """;

        await using var connection = await _connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<DepotMasterRow>(
            new CommandDefinition(
                sql,
                new
                {
                    depot_code = string.IsNullOrWhiteSpace(depotCode) ? null : depotCode.Trim(),
                    depot_name = string.IsNullOrWhiteSpace(depotName) ? null : depotName.Trim(),
                    regn_new = string.IsNullOrWhiteSpace(regnNew) ? null : regnNew.Trim().ToUpperInvariant(),
                    search_text = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim()
                },
                cancellationToken: cancellationToken));

        return rows.Select(r => r.ToRecord()).ToList();
    }

    private sealed class DepotMasterRow
    {
        public string depot_code { get; set; } = "";
        public string depot_name { get; set; } = "";
        public string? regn_new { get; set; }
        public string? depot_state { get; set; }

        public DepotMaster ToRecord()
            => new(depot_code, depot_name, regn_new, depot_state);
    }
}
