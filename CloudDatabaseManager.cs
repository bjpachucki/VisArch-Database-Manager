using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VisArch._StateMachines
{
    public class CloudDatabaseManager<T> : IDatabaseManager<T>
    {
        public Task<int> CountRowsAsync<TData>(TData data) where TData : class
        {
            throw new System.NotImplementedException();
        }

        public Task DeleteAsync<TData>(List<Tuple<string, object>> conditions) where TData : class
        {
            throw new NotImplementedException();
        }

        public Task InsertAllAsync<TData>(IEnumerable<TData> data) where TData : class
        {
            throw new System.NotImplementedException();
        }

        public Task InsertAllOrUpdateAsync<TData>(IEnumerable<TData> data) where TData : class
        {
            throw new NotImplementedException();
        }

        public Task InsertAsync(T data)
        {
            throw new System.NotImplementedException();
        }

        public Task<long> InsertOrUpdateAsync(T data)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(T data)
        {
            throw new NotImplementedException();
        }

        public Task<List<TData>> SelectAllAsync<TData>() where TData : class
        {
            throw new NotImplementedException();
        }

        public Task<List<TData>> SelectByPageAsync<TData>(int startIndex, int pageSize) where TData : class
        {
            throw new System.NotImplementedException();
        }

        public Task<List<TData>> SelectBySingleConditionAsync<TData>(Tuple<string, object> condition) where TData : class, new()
        {
            throw new NotImplementedException();
        }

        public Task<List<TData>> SelectByMultipleConditionsAsync<TData>(List<Tuple<string, object>> conditions) where TData : class, new()
        {
            throw new NotImplementedException();
        }

        public Task<List<TData>> SelectByIdsAsync<TData>(long[] ids) where TData : class, new()
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsValueUniqueAsync<TData>(string columnName, TData value) where TData : IComparable<TData>
        {
            throw new NotImplementedException();
        }

        public Task<long> GetNextIdAsync<TData>(string columnName) where TData : class
        {
            throw new NotImplementedException();
        }

        public Task<List<TData>> SelectByPayTierIdAndPermissionIdsAsync<TData>(long payTierId, long[] permissionIds) where TData : class, new()
        {
            throw new NotImplementedException();
        }

        public Task UpdateColumnAsync(string tableName, string columnName, object data, Tuple<string, object> condition)
        {
            throw new NotImplementedException();
        }

        public Task<List<TTarget>> SelectByJunctionConditionAsync<TSource, TJunction, TTarget>(Tuple<string, object> sourceCondition)
            where TSource : class, new()
            where TJunction : class, new()
            where TTarget : class, new()
        {
            throw new NotImplementedException();
        }

        public Task<long?> GetCurrentIdAsync<TData>(Tuple<string, object> condition) where TData : class, new()
        {
            throw new NotImplementedException();
        }

        public Task CreateJunctionForNewIdAsync<TSource, TJunction, TTarget>(long sourceId, long targetId)
            where TSource : class, new()
            where TJunction : class, new()
            where TTarget : class, new()
        {
            throw new NotImplementedException();
        }

        public Task DeleteWithJunctionsandRelatedAsync<TPrimary>(Tuple<string, object> primaryCondition, Type relatedTableType, params Type[] junctionTables) where TPrimary : class, new()
        {
            throw new NotImplementedException();
        }

        public Task UpdateJunctionForNewIdAsync<TSource, TJunction, TTarget>(long oldId, long newId, long targetId)
            where TSource : class, new()
            where TJunction : class, new()
            where TTarget : class, new()
        {
            throw new NotImplementedException();
        }

        public Task<long> UpdateSpecificRowAsync(T data, long id, string[] updateColumns)
        {
            throw new NotImplementedException();
        }
    }
}


