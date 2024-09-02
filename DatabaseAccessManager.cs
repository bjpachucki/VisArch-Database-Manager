using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VisArch._StateMachines
{
    public class DatabaseAccessManager<T> where T : class
    {
        private readonly SemaphoreSlim _semaphor;

        private IDatabaseManager<T> databaseManager;

        public DatabaseAccessManager(bool useLocalDatabase)
        {
            if (useLocalDatabase)
            {
                _semaphor = new SemaphoreSlim(1, 1);
                databaseManager = new LocalDatabaseManager<T>();
            }
            else
            {
                _semaphor = new SemaphoreSlim(1, 10);
                databaseManager = new CloudDatabaseManager<T>();
            }
        }

        public async Task<int> CountRowsAsync<TData>(TData data) where TData : class
        {
            int result = await databaseManager.CountRowsAsync(data);
            return result;
        }

        public async Task DeleteAsync<TData>(List<Tuple<string, object>> conditions) where TData : class
        {
            await _semaphor.WaitAsync();
            try
            {
                await databaseManager.DeleteAsync<TData>(conditions);
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task DeleteWithJunctionsandRelatedAsync<TPrimary>(Tuple<string, object> primaryCondition, Type relatedTableType, params Type[] junctionTables) where TPrimary : class, new()
        {
            await _semaphor.WaitAsync();
            try
            {
                await databaseManager.DeleteWithJunctionsandRelatedAsync<TPrimary>(primaryCondition, relatedTableType, junctionTables);
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task InsertAllAsync<TData>(IEnumerable<TData> data) where TData : class
        {
            await _semaphor.WaitAsync();
            try
            {
                await databaseManager.InsertAllAsync(data);
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task InsertAllOrUpdateAsync<TData>(IEnumerable<TData> data) where TData : class
        {
            await _semaphor.WaitAsync();
            try
            {
                await databaseManager.InsertAllOrUpdateAsync(data);
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task InsertAsync(T data)
        {
            await _semaphor.WaitAsync();
            try
            {
                await databaseManager.InsertAsync(data);
            }
            finally
            {
                _semaphor.Release();
            }
        }

        public async Task<long> InsertOrUpdateAsync(T data)
        {
            await _semaphor.WaitAsync();
            try
            {
                return await databaseManager.InsertOrUpdateAsync(data);
            }
            finally
            {
                _semaphor.Release();
            }
        }

        public async Task<long> UpdateSpecificRowAsync(T data, long id, string[] updateColumns)
        {
            await _semaphor.WaitAsync();
            try
            {
                return await databaseManager.UpdateSpecificRowAsync(data, id, updateColumns);
            }
            finally
            {
                _semaphor.Release();
            }
        }

        public async Task UpdateAsync(T data)
        {
            await _semaphor.WaitAsync();
            try
            {
                await databaseManager.UpdateAsync(data);
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task UpdateColumnAsync(string tableName, string columnName, object data, Tuple<string, object> condition)
        {
            await _semaphor.WaitAsync();
            try
            {
                await databaseManager.UpdateColumnAsync(tableName, columnName, data, condition);
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task UpdateJunctionForNewIdAsync<TSource, TJunction, TTarget>(long oldId, long newId, long targetId) where TSource : class, new() where TJunction : class, new() where TTarget : class, new()
        {
            await _semaphor.WaitAsync();
            try
            {
                await databaseManager.UpdateJunctionForNewIdAsync< TSource,TJunction,TTarget>(oldId, newId, targetId);
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task<List<TData>> SelectAllAsync<TData>() where TData : class
        {
            await _semaphor.WaitAsync();
            try
            {
                List<TData> results = await databaseManager.SelectAllAsync<TData>();
                return results;
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task<List<TData>> SelectByPageAsync<TData>(int startIndex, int pageSize) where TData : class
        {
            await _semaphor.WaitAsync();
            try
            {
                List<TData> results = await databaseManager.SelectByPageAsync<TData>(startIndex, pageSize);
                return results;
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task<List<TData>> SelectBySingleConditionAsync<TData>(Tuple<string, object> condition) where TData : class, new()
        {
            await _semaphor.WaitAsync();
            try
            {
                List<TData> results = await databaseManager.SelectBySingleConditionAsync<TData>(condition);
                return results;
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task<List<TData>> SelectByMultipleConditionsAsync<TData>(List<Tuple<string, object>> conditions) where TData : class, new()
        {
            await _semaphor.WaitAsync();
            try
            {
                List<TData> results = await databaseManager.SelectByMultipleConditionsAsync<TData>(conditions);
                return results;
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task<List<TData>> SelectByIdsAsync<TData>(long[] ids) where TData : class, new()
        {
            await _semaphor.WaitAsync();
            try
            {
                List<TData> results = await databaseManager.SelectByIdsAsync<TData>(ids);
                return results;
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task<List<TTarget>> SelectByJunctionConditionAsync<TSource, TJunction, TTarget>(Tuple<string, object> sourceCondition)
            where TSource : class, new()
            where TJunction : class, new()
            where TTarget : class, new()
        {
            await _semaphor.WaitAsync();
            try
            {
                List<TTarget> results = await databaseManager.SelectByJunctionConditionAsync<TSource, TJunction, TTarget>(sourceCondition);
                return results;
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task CreateJunctionForNewIdAsync<TSource, TJunction, TTarget>(long sourceId, long targetId) where TSource : class, new() where TJunction : class, new() where TTarget : class, new()
        {
            await _semaphor.WaitAsync();
            try
            {
                await databaseManager.CreateJunctionForNewIdAsync<TSource, TJunction, TTarget>(sourceId, targetId);
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task<bool> IsValueUniqueAsync<TData>(string columnName, TData value) where TData : IComparable<TData>
        {
            await _semaphor.WaitAsync();
            try
            {
                bool results = await databaseManager.IsValueUniqueAsync(columnName, value);
                return results;
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task<long?> GetCurrentIdAsync<TData>(Tuple<string, object> condition) where TData : class, new()
        {
            await _semaphor.WaitAsync();
            try
            {
                long? results = await databaseManager.GetCurrentIdAsync<TData>(condition);
                return results;
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task<long> GetNextIdAsync<TData>(string columnName) where TData : class
        {
            await _semaphor.WaitAsync();
            try
            {
                long results = await databaseManager.GetNextIdAsync<TData>(columnName);
                return results;
            }
            finally
            {
                _semaphor?.Release();
            }
        }

        public async Task<List<TData>> SelectByPayTierIdAndPermissionIdsAsync<TData>(long payTierId, long[] permissionIds) where TData : class, new()
        {
            await _semaphor.WaitAsync();
            try
            {
                List<TData> results = await databaseManager.SelectByPayTierIdAndPermissionIdsAsync<TData>(payTierId, permissionIds);
                return results;
            }
            finally
            {
                _semaphor?.Release();
            }
        }
    }
}



