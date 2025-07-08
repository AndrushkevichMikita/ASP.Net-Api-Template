using System.Linq.Expressions;

namespace ApiTemplate.Application.Interfaces
{
    public interface IRepository<TEntity> where TEntity : class
    {
        /// <summary>
        /// Releases the resources used by the repository.
        /// </summary>
        void Dispose();
        /// <summary>
        /// Gets a queryable collection of the entity with optional no-tracking behavior.
        /// </summary>
        /// <param name="asNoTracking">If true, returns a queryable collection with no-tracking enabled.</param>
        /// <returns>A queryable collection of the entity.</returns>
        IQueryable<TEntity> GetIQueryable(bool asNoTracking = false);
        /// <summary>
        /// Inserts a new entity asynchronously.
        /// </summary>
        /// <param name="entity">The entity to insert.</param>
        /// <param name="saveChanges">If true, saves changes to the database immediately.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The inserted entity.</returns>
        Task<TEntity> InsertAsync(TEntity entity, bool saveChanges = false, CancellationToken cancellationToken = default);
        /// <summary>
        /// Inserts multiple entities asynchronously.
        /// </summary>
        /// <typeparam name="TList">The type of the list containing entities.</typeparam>
        /// <param name="entities">The list of entities to insert.</param>
        /// <param name="saveChanges">If true, saves changes to the database immediately.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The list of inserted entities.</returns>
        Task<TList> InsertAsync<TList>(TList entities, bool saveChanges = false, CancellationToken cancellationToken = default) where TList : IList<TEntity>;
        /// <summary>
        /// Updates an entity asynchronously.
        /// </summary>
        /// <param name="entity">The entity to update.</param>
        /// <param name="saveChanges">If true, saves changes to the database immediately.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="fields">Optional fields to update in the entity.</param>
        /// <returns>The updated entity.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity does not exist in the database.</exception>
        Task<TEntity> UpdateAsync(TEntity entity, bool saveChanges = false, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] fields);
        /// <summary>
        /// Updates multiple entities asynchronously.
        /// </summary>
        /// <param name="entities">The list of entities to update.</param>
        /// <param name="saveChanges">If true, saves changes to the database immediately.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="fields">Update only this fields if provided.</param>
        /// <returns>The list of updated entities.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the list of entities is null.</exception>
        Task<List<TEntity>> UpdateAsync(List<TEntity> entities, bool saveChanges = false, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] fields);
        /// <summary>
        /// Deletes an entity asynchronously.
        /// </summary>
        /// <param name="entity">The entity to delete.</param>
        /// <param name="saveChanges">If true, saves changes to the database immediately.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        Task DeleteAsync(TEntity entity, bool saveChanges = false, CancellationToken cancellationToken = default);
        /// <summary>
        /// Deletes multiple entities asynchronously.
        /// </summary>
        /// <typeparam name="TList">The type of the list containing entities.</typeparam>
        /// <param name="items">The list of entities to delete.</param>
        /// <param name="saveChanges">If true, saves changes to the database immediately.</param>
        /// <param name="offBulk">If true, disables bulk operations.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        Task DeleteAsync<TList>(TList items, bool saveChanges = false, bool offBulk = false, CancellationToken cancellationToken = default) where TList : IList<TEntity>;
    }
}
