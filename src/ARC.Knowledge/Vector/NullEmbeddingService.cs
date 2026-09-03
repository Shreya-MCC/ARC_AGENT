namespace ARC.Knowledge.Vector;

/// <summary>Fallback when embedding endpoint is not configured — dense search is skipped.</summary>
public sealed class NullEmbeddingService : IEmbeddingService
{
    public bool IsConfigured => false;

    public Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken)
        => Task.FromResult<float[]?>(null);
}
