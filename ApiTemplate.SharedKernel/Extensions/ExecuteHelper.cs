using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApiTemplate.SharedKernel.Extensions
{
    public static class ExecuteHelper<T>
    {
        /// <returns> bool =  @do executed at least once ? </returns>
        public static async Task<bool> ExecuteWhileAsync(Func<int, int, Task<List<T>>> getCnt, Func<List<T>, Task> @do, int cntToTake)
        {
            int cntToSkip = 0;
            bool execAtLeastOnce = false;
            var items = await getCnt(cntToSkip, cntToTake);

            while (items.Count > 0)
            {
                await @do(items);
                cntToSkip += cntToTake;
                execAtLeastOnce = true;
                items = await getCnt(cntToSkip, cntToTake);
            }

            return execAtLeastOnce;
        }
    }
}
