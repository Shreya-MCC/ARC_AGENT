using Microsoft.Azure.Cosmos;
using ARC.Data.Cosmos;
using ARC.Knowledge.Exceptions;
using ARC.Knowledge.Vector;

namespace ARC.Knowledge.Lexical;

/// <summary>Loads active Cosmos documents so the Lucene index can be rebuilt.</summary>
public sealed class CosmosLexicalCorpus : ILexicalCorpus
{
    private readonly Container _documents;

    public CosmosLexicalCorpus(ICosmosClientFactory cosmos)
    {
        _documents = cosmos.Documents;
    }

    public async Task<IReadOnlyList<IndexedDocument>> GetActiveDocumentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                SELECT c.id, c.title, c.content, c.status, c.documentCategory, c.version,
                       c.regionScope, c.blobLocation
                FROM c
                WHERE c.status = 'ACTIVE'
                  AND (NOT IS_DEFINED(c.docType) OR (c.docType != 'chatMessage' AND c.docType != 'chatSessionPointer'))
                """;

            var results = new List<IndexedDocument>();
            using var iterator = _documents.GetItemQueryIterator<IndexedDocument>(new QueryDefinition(sql));
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(page);
            }

            return results;
        }
        catch (Exception ex)
        {
            throw new RetrievalFailedException("Failed to load documents for the Lucene lexical index.", ex);
        }
    }
}
