using ApiTemplate.Domain.Entities;
using ApiTemplate.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApiTemplate.Infrastructure.Tests
{
    public class TRepositoryTests
    {
        private readonly TRepository<Account> _repository;
        private readonly ApplicationDbContext _context;

        public TRepositoryTests()
        {
            // Create a new options instance for each test
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _repository = new TRepository<Account>(_context);
        }

        private static Account AccountEntity() => new() { FirstName = "Initial", LastName = "Initial" };

        private static readonly List<Account> AccountEntities = new() { AccountEntity(), AccountEntity() };

        [Fact]
        public async Task GetIQueryable_ReturnsQueryable()
        {
            await _repository.InsertAsync(AccountEntities, true);

            var result = await _repository.GetIQueryable().ToListAsync();

            Assert.False(result.All(c => _context.Entry(c).State == EntityState.Detached));
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetIQueryable_AsNoTracking_ReturnsQueryable()
        {
            await _repository.InsertAsync(AccountEntities, true);

            var result = await _repository.GetIQueryable(true).ToListAsync();

            Assert.True(result.All(c => _context.Entry(c).State == EntityState.Detached));
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task InsertAsync_AddsEntityToContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntity(), true);
            Assert.NotNull(inserted);

            var result = await _context.Set<Account>().FindAsync(1);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task InsertAsync_MultipleEntities_AddsEntitiesToContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntities, true);
            Assert.Equal(2, inserted.Count);

            var result = await _context.Set<Account>().ToListAsync();
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task DeleteAsync_RemovesEntityFromContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntity(), true);
            await _repository.DeleteAsync(inserted, true);

            var result = await _context.Set<Account>().FindAsync(1);
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteAsync_Detached_RemovesEntityFromContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntity(), true);

            _context.ChangeTracker.Clear();

            await _repository.DeleteAsync(inserted, true);

            var result = await _context.Set<Account>().FindAsync(1);
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteAsync_MultipleEntities_RemovesEntitiesFromContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntities, true);

            await _repository.DeleteAsync(inserted, true);

            var result = await _context.Set<Account>().ToListAsync();
            Assert.Empty(result);
        }

        [Fact]
        public async Task DeleteAsync_MultipleEntities_Detached_RemovesEntitiesFromContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntities, true);

            _context.ChangeTracker.Clear();

            await _repository.DeleteAsync(inserted, true);

            var result = await _context.Set<Account>().ToListAsync();
            Assert.Empty(result);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesEntityInContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntity(), true);

            inserted.FirstName = "First update";
            await _repository.UpdateAsync(inserted, true);
            Assert.Equal("First update", inserted.FirstName);

            inserted.FirstName = "Second update";
            await _repository.UpdateAsync(inserted, true);
            Assert.Equal("Second update", inserted.FirstName);

            var result = await _context.Set<Account>().FindAsync(1);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task UpdateAsync_Detached_UpdatesEntityInContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntity(), true);

            _context.ChangeTracker.Clear();

            inserted.FirstName = "First update";
            await _repository.UpdateAsync(inserted, true);
            Assert.Equal("First update", inserted.FirstName);

            _context.ChangeTracker.Clear();

            inserted.FirstName = "Second update";
            await _repository.UpdateAsync(inserted, true);
            Assert.Equal("Second update", inserted.FirstName);

            var result = await _context.Set<Account>().FindAsync(1);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task UpdateAsync_SpecificFields_UpdatesEntityFieldsInContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntity(), true);

            inserted.FirstName = "First update";
            inserted.LastName = "First update";
            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None, e => e.FirstName);
            Assert.Equal("First update", inserted.FirstName);
            Assert.NotEqual("First update", inserted.LastName);

            inserted.FirstName = "Second update";
            inserted.LastName = "Second update";
            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None, e => e.FirstName);
            Assert.Equal("Second update", inserted.FirstName);
            Assert.NotEqual("Second update", inserted.LastName);

            var result = await _context.Set<Account>().FindAsync(1);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task UpdateAsync_SpecificFields_Detached_UpdatesEntityFieldsInContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntity(), true);

            _context.ChangeTracker.Clear();

            inserted.FirstName = "First update";
            inserted.LastName = "First update";
            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None, e => e.FirstName);
            Assert.Equal("First update", inserted.FirstName);
            Assert.NotEqual("First update", inserted.LastName);

            _context.ChangeTracker.Clear();

            inserted.FirstName = "Second update";
            inserted.LastName = "Second update";
            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None, e => e.FirstName);
            Assert.Equal("Second update", inserted.FirstName);
            Assert.NotEqual("Second update", inserted.LastName);

            var result = await _context.Set<Account>().FindAsync(1);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task UpdateAsync_MultipleEntities_UpdatesEntitiesInContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntities, true);

            inserted.ForEach(x =>
            {
                x.FirstName = "First update";
            });

            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None);
            Assert.True(inserted.All(x => x.FirstName == "First update"));

            inserted.ForEach(x =>
            {
                x.FirstName = "Second update";
            });

            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None);
            Assert.True(inserted.All(x => x.FirstName == "Second update"));

            var result = await _context.Set<Account>().ToListAsync();
            Assert.NotNull(result);
            Assert.True(result.Count == inserted.Count);
        }

        [Fact]
        public async Task UpdateAsync_MultipleEntities_Detached_UpdatesEntitiesInContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntities, true);

            _context.ChangeTracker.Clear();

            inserted.ForEach(x =>
            {
                x.FirstName = "First update";
            });

            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None);
            Assert.True(inserted.All(x => x.FirstName == "First update"));

            _context.ChangeTracker.Clear();

            inserted.ForEach(x =>
            {
                x.FirstName = "Second update";
            });

            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None);
            Assert.True(inserted.All(x => x.FirstName == "Second update"));

            var result = await _context.Set<Account>().ToListAsync();
            Assert.NotNull(result);
            Assert.True(result.Count == inserted.Count);
        }

        [Fact]
        public async Task UpdateAsync_MultipleEntities_SpecificFields_UpdatesEntityFieldsInContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntities, true);

            inserted.ForEach(x =>
            {
                x.FirstName = "First update";
                x.LastName = "First update";
            });

            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None, e => e.FirstName);
            Assert.True(inserted.All(x => x.FirstName == "First update"));
            Assert.True(inserted.All(x => x.LastName != "First update"));

            inserted.ForEach(x =>
            {
                x.FirstName = "Second update";
                x.LastName = "Second update";
            });

            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None, e => e.FirstName);
            Assert.True(inserted.All(x => x.FirstName == "Second update"));
            Assert.True(inserted.All(x => x.LastName != "Second update"));

            var result = await _context.Set<Account>().ToListAsync();
            Assert.NotNull(result);
            Assert.True(result.Count == inserted.Count);
        }

        [Fact]
        public async Task UpdateAsync_MultipleEntities_Detached_SpecificFields_UpdatesEntityFieldsInContext()
        {
            var inserted = await _repository.InsertAsync(AccountEntities, true);

            _context.ChangeTracker.Clear();

            inserted.ForEach(x =>
            {
                x.FirstName = "First update";
                x.LastName = "First update";
            });

            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None, e => e.FirstName);
            Assert.True(inserted.All(x => x.FirstName == "First update"));
            Assert.True(inserted.All(x => x.LastName != "First update"));

            _context.ChangeTracker.Clear();

            inserted.ForEach(x =>
            {
                x.FirstName = "Second update";
                x.LastName = "Second update";
            });

            inserted = await _repository.UpdateAsync(inserted, true, CancellationToken.None, e => e.FirstName);
            Assert.True(inserted.All(x => x.FirstName == "Second update"));
            Assert.True(inserted.All(x => x.LastName != "Second update"));

            var result = await _context.Set<Account>().ToListAsync();
            Assert.NotNull(result);
            Assert.True(result.Count == inserted.Count);
        }

        [Fact]
        public void Dispose_DisposesContext()
        {
            _repository.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _context.Set<Account>().Find(1));
        }
    }
}