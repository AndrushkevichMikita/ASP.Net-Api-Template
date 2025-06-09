using ApiTemplate.SharedKernel.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ApiTemplate.SharedKernel.Tests
{
    public class ExecuteHelperTests
    {
        [Fact]
        public async Task ExecuteWhileAsync_ProcessesFinalSingleItemBatch()
        {
            // Arrange
            var source = new List<int> { 1, 2, 3, 4, 5 };
            var processed = new List<int>();

            async Task<List<int>> GetItems(int skip, int take)
            {
                return await Task.FromResult(source.Skip(skip).Take(take).ToList());
            }

            async Task DoAsync(List<int> items)
            {
                processed.AddRange(items);
                await Task.CompletedTask;
            }

            // Act
            var executed = await ExecuteHelper<int>.ExecuteWhileAsync(GetItems, DoAsync, 2);

            // Assert
            Assert.True(executed);
            Assert.Equal(source, processed);
        }
    }
}
