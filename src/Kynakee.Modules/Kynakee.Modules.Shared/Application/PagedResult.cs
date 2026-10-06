namespace Kynakee.Modules.SharedKernel.Application
{
    public static class PagedResultFactory
    {
        public static PagedResult<T> Create<T>(
            IReadOnlyList<T> items,
            int page,
            int pageSize,
            int totalCount)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

            return new PagedResult<T>(items, page, pageSize, totalCount);
        }

        public static PagedResult<T> Empty<T>(int page = 1, int pageSize = 20)
        {
            return new([], page, pageSize, 0);
        }
    }

    /// <summary>
    /// Represents a paginated result set for list queries.
    /// Use this as the return type for any query that returns a collection
    /// that may need pagination (projects list, work items, APUs, etc.).
    ///
    /// Usage in query handler:
    ///   var items = await _db.Projects
    ///       .Where(p => p.TenantId == tenantId)
    ///       .Skip((page - 1) * pageSize)
    ///       .Take(pageSize)
    ///       .Select(p => new ProjectSummaryDto(...))
    ///       .ToListAsync(ct);
    ///
    ///   var total = await _db.Projects
    ///       .Where(p => p.TenantId == tenantId)
    ///       .CountAsync(ct);
    ///
    ///   return PagedResult{ProjectSummaryDto}.Create(items, page, pageSize, total);
    /// </summary>
    /// <typeparam name="T">The type of items in the result set.</typeparam>
    public sealed class PagedResult<T>
    {
        // ── Properties ────────────────────────────────────────────────────────────

        /// <summary>The items in the current page.</summary>
        public IReadOnlyList<T> Items { get; }

        /// <summary>Current page number (1-based).</summary>
        public int Page { get; }

        /// <summary>Number of items per page.</summary>
        public int PageSize { get; }

        /// <summary>Total number of items across all pages.</summary>
        public int TotalCount { get; }

        /// <summary>Total number of pages.</summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        /// <summary>Indicates whether there is a previous page.</summary>
        public bool HasPreviousPage => Page > 1;

        /// <summary>Indicates whether there is a next page.</summary>
        public bool HasNextPage => Page < TotalPages;

        // ── Constructor ───────────────────────────────────────────────────────────

        internal PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
        {
            Items = items;
            Page = page;
            PageSize = pageSize;
            TotalCount = totalCount;
        }

        
    }
}
