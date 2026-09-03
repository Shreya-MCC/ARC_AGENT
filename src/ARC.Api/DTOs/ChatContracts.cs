namespace ARC.Api.DTOs;

public sealed record ChatMessageRequest
{
    public required string Message { get; init; }
    public string? DealerUrn { get; init; }
    public string? CycleId { get; init; }
    public string? Region { get; init; }
}

public sealed record ChatMessageResponse
{
    public required string Reply { get; init; }
    public required string Agent { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    /// <summary>text or html — html is used for table/grid depot results.</summary>
    public string ReplyFormat { get; init; } = "text";
    public object? Data { get; init; }
}
