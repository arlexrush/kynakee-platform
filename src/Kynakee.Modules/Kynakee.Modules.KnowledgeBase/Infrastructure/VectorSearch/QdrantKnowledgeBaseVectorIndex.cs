using Kynakee.Modules.KnowledgeBase.Application.Abstractions;
using Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Kynakee.Modules.KnowledgeBase.Infrastructure.VectorSearch;

/// <summary>
/// Índice vectorial basado en Qdrant que implementa IKnowledgeBaseVectorIndex para almacenar, actualizar, buscar y
/// eliminar embeddings de conceptos canónicos y plantillas APU de forma asíncrona.
/// </summary>
/// <remarks>Opera con un tamaño de vector fijo de 768; valida que los embeddings contengan valores finitos y que
/// el parámetro limit esté entre 1 y 100. Crea colecciones cuando no existen y utiliza QdrantClient para Upsert, Query
/// y Delete. Almacena metadatos en el payload (entity_id, canonical_concept_id, project_type, region) y genera
/// identificadores de punto estables mediante SHA256. Todas las operaciones aceptan CancellationToken y usan
/// ConfigureAwait(false).</remarks>
public sealed class QdrantKnowledgeBaseVectorIndex : IKnowledgeBaseVectorIndex
{
    private const uint VectorSize = 768; // The size of the embedding vector used for canonical concepts and APU templates.
    private readonly QdrantClient _client; // The Qdrant client used to interact with the Qdrant vector database.
    private readonly KnowledgeBaseVectorOptions _options; // The options for configuring the Qdrant vector index, including collection names.

    public QdrantKnowledgeBaseVectorIndex(
        QdrantClient client,
        IOptions<KnowledgeBaseVectorOptions> options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Inserta o actualiza la representación vectorial de un concepto canónico en la colección configurada del servicio
    /// de vectores.
    /// </summary>
    /// <remarks>Valida los argumentos, garantiza la existencia de la colección, construye la entidad de punto
    /// con su payload y realiza un upsert a través del cliente de vectores.</remarks>
    /// <param name="canonicalConceptId">Identificador canónico único del concepto.</param>
    /// <param name="embedding">Vector de incrustación que representa semánticamente el concepto.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Tarea que se completa cuando la operación de inserción/actualización finaliza.</returns>
    public async Task UpsertCanonicalConceptAsync(
        string canonicalConceptId,
        float[] embedding,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalConceptId);
        ValidateEmbedding(embedding);
        var conceptId = new CanonicalConceptId(canonicalConceptId);
        var pointId = StablePointId(conceptId.Value);

        await EnsureCollectionAsync(_options.CanonicalConceptsCollection, cancellationToken)
            .ConfigureAwait(false);

        var point = new PointStruct
        {
            Id = pointId,
            Vectors = embedding,
            Payload = { ["entity_id"] = conceptId.Value }
        };

        await _client.UpsertAsync(
            _options.CanonicalConceptsCollection,
            [point],
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Busca conceptos canónicos similares al embedding proporcionado en la colección configurada y devuelve
    /// coincidencias con su identificador de entidad y puntuación.
    /// </summary>
    /// <remarks>Valida el embedding y el límite antes de consultar; si la colección no existe devuelve una
    /// lista vacía. Filtra puntos sin un 'entity_id' válido o vacío.</remarks>
    /// <param name="embedding">Vector de incrustación usado para la búsqueda de similitud.</param>
    /// <param name="limit">Número máximo de coincidencias a devolver.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asíncrona.</param>
    /// <returns>Lista de solo lectura de KnowledgeBaseVectorMatch con el identificador de entidad y la puntuación de similitud
    /// por coincidencia.</returns>
    public async Task<IReadOnlyList<KnowledgeBaseVectorMatch>> SearchCanonicalConceptsAsync(
        float[] embedding,
        int limit,
        CancellationToken cancellationToken)
    {
        ValidateEmbedding(embedding);
        ValidateLimit(limit);
        if (!await CollectionExistsAsync(
                _options.CanonicalConceptsCollection,
                cancellationToken).ConfigureAwait(false))
        {
            return Array.Empty<KnowledgeBaseVectorMatch>();
        }

        var response = await _client.QueryAsync(
            _options.CanonicalConceptsCollection,
            embedding,
            limit: (ulong)limit,
            payloadSelector: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response
            .Where(point => point.Payload.TryGetValue("entity_id", out var entityId) &&
                            !string.IsNullOrWhiteSpace(entityId.StringValue))
            .Select(point => new KnowledgeBaseVectorMatch(
                point.Payload["entity_id"].StringValue,
                point.Score))
            .ToArray();
    }

    public async Task RemoveCanonicalConceptAsync(
        string canonicalConceptId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalConceptId);
        var conceptId = new CanonicalConceptId(canonicalConceptId);
        if (!await CollectionExistsAsync(_options.CanonicalConceptsCollection, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        await _client.DeleteAsync(
            _options.CanonicalConceptsCollection,
            [StablePointId(conceptId.Value)],
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertAPUTemplateAsync(
        Guid apuTemplateId,
        float[] embedding,
        string canonicalConceptId,
        string projectType,
        string region,
        CancellationToken cancellationToken)
    {
        if (apuTemplateId == Guid.Empty)
        {
            throw new ArgumentException("APU template id cannot be empty.", nameof(apuTemplateId));
        }

        ValidateEmbedding(embedding);
        var conceptId = new CanonicalConceptId(canonicalConceptId);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        var pointId = StablePointId(apuTemplateId);

        await EnsureCollectionAsync(_options.ApuStructuresCollection, cancellationToken)
            .ConfigureAwait(false);

        var point = new PointStruct
        {
            Id = pointId,
            Vectors = embedding,
            Payload =
            {
                ["entity_id"] = apuTemplateId.ToString("D"),
                ["canonical_concept_id"] = conceptId.Value,
                ["project_type"] = projectType,
                ["region"] = region
            }
        };

        await _client.UpsertAsync(
            _options.ApuStructuresCollection,
            [point],
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<KnowledgeBaseVectorMatch>> SearchAPUTemplatesAsync(
        float[] embedding,
        int limit,
        CancellationToken cancellationToken)
    {
        ValidateEmbedding(embedding);
        ValidateLimit(limit);
        if (!await CollectionExistsAsync(
                _options.ApuStructuresCollection,
                cancellationToken).ConfigureAwait(false))
        {
            return Array.Empty<KnowledgeBaseVectorMatch>();
        }

        var response = await _client.QueryAsync(
            _options.ApuStructuresCollection,
            embedding,
            limit: (ulong)limit,
            payloadSelector: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response
            .Where(point => point.Payload.TryGetValue("entity_id", out var entityId) &&
                            !string.IsNullOrWhiteSpace(entityId.StringValue))
            .Select(point => new KnowledgeBaseVectorMatch(
                point.Payload["entity_id"].StringValue,
                point.Score))
            .ToArray();
    }

    public async Task RemoveAPUTemplateAsync(
        Guid apuTemplateId,
        CancellationToken cancellationToken)
    {
        if (apuTemplateId == Guid.Empty)
        {
            throw new ArgumentException("APU template id cannot be empty.", nameof(apuTemplateId));
        }

        if (!await CollectionExistsAsync(_options.ApuStructuresCollection, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        await _client.DeleteAsync(
            _options.ApuStructuresCollection,
            [StablePointId(apuTemplateId)],
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureCollectionAsync(
        string collectionName,
        CancellationToken cancellationToken)
    {
        if (await CollectionExistsAsync(collectionName, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await _client.CreateCollectionAsync(
            collectionName,
            new VectorParams { Size = VectorSize, Distance = Distance.Cosine },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private Task<bool> CollectionExistsAsync(
        string collectionName,
        CancellationToken cancellationToken) =>
        _client.CollectionExistsAsync(collectionName, cancellationToken);

    private static void ValidateEmbedding(float[] embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        if (!EmbeddingValidation.IsValid(embedding))
        {
            throw new ArgumentException("Embedding must contain 768 finite values.", nameof(embedding));
        }
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Search limit must be between 1 and 100.");
        }
    }

    private static PointId StablePointId(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        return StablePointId(new Guid(bytes.AsSpan(0, 16)));
    }

    private static PointId StablePointId(Guid value) => new() { Uuid = value.ToString("D") };
}
