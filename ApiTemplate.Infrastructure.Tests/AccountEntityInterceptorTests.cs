using ApiTemplate.Domain.Entities;
using ApiTemplate.Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace ApiTemplate.Infrastructure.Tests
{
    public class AccountEntityInterceptorTests
    {
        private DbContextOptions<ApplicationDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(new AccountEntityInterceptor())
                .Options;
        }

        [Fact]
        public void SavingChanges_ShouldUpdateEntities()
        {
            // Arrange
            using var context = new ApplicationDbContext(CreateOptions());
            var accountEntity = new Account { FirstName = "Test1", LastName = "Test1" };

            // Act
            context.Add(accountEntity);
            context.SaveChanges();

            // Assert
            Assert.NotEqual(default, accountEntity.Created);
            Assert.NotEqual(default, accountEntity.LastUpdated);
        }

        [Fact]
        public async Task SavingChangesAsync_ShouldUpdateEntities()
        {
            // Arrange
            using var context = new ApplicationDbContext(CreateOptions());
            var accountEntity = new Account { FirstName = "Test1", LastName = "Test1" };

            // Act
            context.Add(accountEntity);
            await context.SaveChangesAsync();

            // Assert
            Assert.NotEqual(default, accountEntity.Created);
            Assert.NotEqual(default, accountEntity.LastUpdated);
        }

        [Fact]
        public void UpdateEntities_ShouldUpdateLastUpdated_ForModifiedEntities()
        {
            // Arrange
            using var context = new ApplicationDbContext(CreateOptions());
            var accountEntity = new Account { FirstName = "Test1", LastName = "Test1" };

            // Act
            context.Add(accountEntity);
            context.SaveChanges();
            accountEntity.LastName = "Updated";
            context.SaveChanges();

            // Assert
            Assert.NotEqual(default, accountEntity.Created);
            Assert.NotEqual(default, accountEntity.LastUpdated);
            Assert.NotEqual(accountEntity.Created, accountEntity.LastUpdated);
        }
    }
}
