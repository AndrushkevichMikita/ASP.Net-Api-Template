using ApiTemplate.Application.Interfaces;
using ApiTemplate.SharedKernel.PrimitivesExtensions;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace ApiTemplate.Infrastructure.Repositories
{
    public class TRepository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        /// <summary>
        /// Min value when bulk operation must be applied
        /// </summary>
        const int bulkOperationStartCount = 5;

        private DbSet<TEntity> _dbSet;
        private ApplicationDbContext Context { get; set; }
        private static readonly ConcurrentDictionary<Type, IProperty[]> _keyPropertiesCache = new();
        private static readonly ConcurrentDictionary<Type, Func<TEntity, object[]>> _primaryKeyAccessorCache = new();

        public TRepository(ApplicationDbContext appContext)
        {
            Context = appContext;
        }

        protected DbSet<TEntity> DbSet
        {
            get
            {
                _dbSet ??= Context.Set<TEntity>();
                return _dbSet;
            }
        }

        private IProperty[] GetPrimaryKeyProperties()
        {
            return _keyPropertiesCache.GetOrAdd(typeof(TEntity), entityType =>
            {
                var keyProperties = Context.Model.FindEntityType(entityType).FindPrimaryKey().Properties;
                return keyProperties.ToArray();
            });
        }

        private Func<TEntity, object[]> GetPrimaryKeyValuesFunc()
        {
            return _primaryKeyAccessorCache.GetOrAdd(typeof(TEntity), entityType =>
            {
                // Get the key properties (this will use the cache)
                var keyProperties = GetPrimaryKeyProperties();

                // Create the accessors for getting primary key values
                var accessors = keyProperties.Select(p => (Func<TEntity, object>)((entity) => p.PropertyInfo.GetValue(entity))).ToArray();

                // Return a delegate that retrieves primary key values using the accessors
                return (entity) => accessors.Select(a => a(entity)).ToArray();
            });
        }

        public virtual IQueryable<TEntity> GetIQueryable(bool asNoTracking = false)
        {
            if (asNoTracking)
                return DbSet.AsQueryable().AsNoTracking();

            return DbSet.AsQueryable();
        }

        public virtual async Task<TEntity> InsertAsync(TEntity entity, bool saveChanges = false, CancellationToken cancellationToken = default)
        {
            var toReturn = await DbSet.AddAsync(entity, cancellationToken);

            if (saveChanges)
                await Context.SaveChangesAsync(cancellationToken);

            return toReturn.Entity;
        }

        public virtual async Task<TList> InsertAsync<TList>(TList entities, bool saveChanges = false, CancellationToken cancellationToken = default) where TList : IList<TEntity>
        {
            await DbSet.AddRangeAsync(entities, cancellationToken);

            if (saveChanges)
                await Context.SaveChangesAsync(cancellationToken);

            return entities;
        }

        public virtual async Task DeleteAsync(TEntity entity, bool saveChanges = false, CancellationToken cancellationToken = default)
        {
            DbSet.Remove(entity);

            if (saveChanges)
                await Context.SaveChangesAsync(cancellationToken);
        }

        public virtual async Task DeleteAsync<TList>(TList items, bool saveChanges = false, bool offBulk = false, CancellationToken cancellationToken = default) where TList : IList<TEntity>
        {
            if (items == null || items.Count < 1)
                return;

            if (items.Count >= bulkOperationStartCount && !offBulk)
            {
                await Context.BulkDeleteAsync(items, cancellationToken: cancellationToken);
            }
            else
            {
                DbSet.RemoveRange(items);
            }

            if (saveChanges)
                await Context.SaveChangesAsync(cancellationToken);
        }

        public virtual async Task<TEntity> UpdateAsync(TEntity entity, bool saveChanges = false, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] fields)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            if (fields.Length == 0)
            {
                DbSet.Update(entity);
            }
            else
            {
                var entry = Context.Entry(entity);
                // Attach the entity if not already tracked
                if (entry.State == EntityState.Detached)
                {
                    var getPrimaryKeyValuesFunc = GetPrimaryKeyValuesFunc();
                    var keyValues = getPrimaryKeyValuesFunc(entity);
                    var existingEntity = await DbSet.FindAsync(keyValues);

                    if (existingEntity != null)
                    {
                        Context.Entry(existingEntity).CurrentValues.SetValues(entity);
                        entry = Context.Entry(existingEntity);
                        // Ensure we are updating the tracked entity, not the detached one
                        entity = entry.Entity;
                    }
                    else
                    {
                        throw new InvalidOperationException("Entity does not exist in the database.");
                    }
                }
                foreach (var property in entry.Properties)
                {
                    property.IsModified = fields.Any(f => property.Metadata.Name == LambdaExtension.GetMemberName(f));
                }
            }

            if (saveChanges)
            {
                await Context.SaveChangesAsync(cancellationToken);
            }

            return entity;
        }

        private async Task<List<TEntity>> FindEntitiesAsync(IEnumerable<object[]> keyValuesList)
        {
            // Convert key values to a dictionary for easier comparison
            var keyValuesDict = keyValuesList.ToDictionary(
                keyValues => string.Join(",", keyValues),
                keyValues => keyValues
            );

            var getPrimaryKeyValuesFunc = GetPrimaryKeyValuesFunc();
            // Get the local entities using cached accessors
            var localEntities = DbSet.Local.Where(entity =>
            {
                var entityKeyValues = getPrimaryKeyValuesFunc(entity); // Use cached accessors
                var key = string.Join(",", entityKeyValues);
                return keyValuesDict.ContainsKey(key);
            }).ToList();

            // Remove found keys from the dictionary
            localEntities.ForEach(localEntity =>
            {
                var entityKeyValues = getPrimaryKeyValuesFunc(localEntity); // Use cached accessors
                var key = string.Join(",", entityKeyValues);
                keyValuesDict.Remove(key);
            });

            // Remaining keys need to be queried from the database
            if (keyValuesDict.Count > 0)
            {
                var parameter = Expression.Parameter(typeof(TEntity));
                Expression predicate = Expression.Constant(false);
                var keyProperties = GetPrimaryKeyProperties().ToList(); // Reuse the cached key properties

                foreach (var keyValues in keyValuesDict.Values)
                {
                    Expression keyPredicate = Expression.Constant(true);
                    for (int i = 0; i < keyProperties.Count; i++)
                    {
                        var keyProperty = keyProperties[i];
                        var keyValue = Expression.Constant(keyValues[i]);
                        var propertyAccess = Expression.Property(parameter, keyProperty.Name);
                        var equality = Expression.Equal(propertyAccess, keyValue);
                        keyPredicate = Expression.AndAlso(keyPredicate, equality);
                    }
                    predicate = Expression.OrElse(predicate, keyPredicate);
                }

                var lambda = Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);
                var queriedEntities = await DbSet.Where(lambda).ToListAsync();
                localEntities.AddRange(queriedEntities);
            }

            return localEntities;
        }

        public virtual async Task<List<TEntity>> UpdateAsync(List<TEntity> entities, bool saveChanges = false, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] fields)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var toReturnEntities = entities;

            var isPartialUpdate = fields.Length > 0;
            if (entities.Count >= bulkOperationStartCount)
            {
                await Context.BulkUpdateAsync(entities,
                                              isPartialUpdate ? options => options.PropertiesToInclude = fields.Select(LambdaExtension.GetMemberName).ToList()
                                                              : null);
            }
            else
            {
                if (!isPartialUpdate)
                {
                    DbSet.UpdateRange(entities);
                }
                else
                {
                    var keyProperties = GetPrimaryKeyProperties();
                    var getPrimaryKeyValuesFunc = GetPrimaryKeyValuesFunc();
                    var keyValuesList = entities.Select(getPrimaryKeyValuesFunc).ToList();
                    var existingEntities = await FindEntitiesAsync(keyValuesList);

                    var keyToEntityMap = existingEntities.ToDictionary(
                                         existingEntity => string.Join(",", keyProperties.Select(p => p.PropertyInfo.GetValue(existingEntity))),
                                         existingEntity => existingEntity);

                    toReturnEntities = entities.Select((entity, index) =>
                    {
                        var keyValues = keyValuesList[index];
                        var key = string.Join(",", keyValues);

                        // Lookup the existing entity using the key
                        if (keyToEntityMap.TryGetValue(key, out var existingEntity))
                        {
                            // Update the existing entity with the current entity's values
                            Context.Entry(existingEntity).CurrentValues.SetValues(entity);
                        }
                        else
                        {
                            // Attach the new entity
                            DbSet.Attach(entity);
                            existingEntity = entity;
                        }

                        // Mark properties as modified for partial updates
                        var entry = Context.Entry(existingEntity);
                        foreach (var property in entry.Properties)
                        {
                            property.IsModified = fields.Any(f => property.Metadata.Name == LambdaExtension.GetMemberName(f));
                        }
                        // link to existingEntity that will later be changed in the foreach loop,
                        // properties that should not change will be returned to their previous state
                        return entry.Entity;
                    }).ToList();
                }
            }

            if (saveChanges)
                await Context.SaveChangesAsync(cancellationToken);

            return toReturnEntities;
        }

        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Context.Dispose();
                }
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
            GC.SuppressFinalize(this);
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        }
    }
}
