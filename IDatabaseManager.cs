using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VisArch._StateMachines
{
    public interface IDatabaseManager<T>
    {
        Task<int> CountRowsAsync<TData>(TData data) where TData : class;

        Task DeleteWithJunctionsandRelatedAsync<TPrimary>(Tuple<string, object> primaryCondition, Type relatedTableType, params Type[] junctionTables) where TPrimary : class, new();
        Task DeleteAsync<TData>(List<Tuple<string, object>> conditions) where TData : class;

        Task InsertAllAsync<TData>(IEnumerable<TData> data) where TData : class;
        Task InsertAsync(T data);
        Task<long> InsertOrUpdateAsync(T data);
        Task<long> UpdateSpecificRowAsync(T data, long id, string[] updateColumns);
        Task InsertAllOrUpdateAsync<TData>(IEnumerable<TData> data) where TData : class;

        Task UpdateAsync(T data);
        Task UpdateColumnAsync(string tableName, string columnName, object data, Tuple<string, object> condition);
        Task UpdateJunctionForNewIdAsync<TSource, TJunction, TTarget>(long oldId, long newId, long targetId) where TSource : class, new() where TJunction : class, new() where TTarget : class, new();

        Task<List<TData>> SelectAllAsync<TData>() where TData : class;
        Task<List<TData>> SelectByPageAsync<TData>(int startIndex, int pageSize) where TData : class;
        Task<List<TData>> SelectBySingleConditionAsync<TData>(Tuple<string, object> condition) where TData : class, new();
        Task<List<TData>> SelectByMultipleConditionsAsync<TData>(List<Tuple<string, object>> conditions) where TData : class, new();
        Task<List<TData>> SelectByIdsAsync<TData>(long[] ids) where TData : class, new();

        Task<List<TTarget>> SelectByJunctionConditionAsync<TSource, TJunction, TTarget>(Tuple<string, object> sourceCondition) where TSource : class, new() where TJunction : class, new() where TTarget : class, new();
        Task CreateJunctionForNewIdAsync<TSource, TJunction, TTarget>(long sourceId, long targetId) where TSource : class, new() where TJunction : class, new() where TTarget : class, new();

        Task<bool> IsValueUniqueAsync<TData>(string columnName, TData value) where TData : IComparable<TData>;
        Task<long> GetNextIdAsync<TData>(string columnName) where TData : class;
        Task<long?> GetCurrentIdAsync<TData>(Tuple<string, object> condition) where TData : class, new();
        Task<List<TData>> SelectByPayTierIdAndPermissionIdsAsync<TData>(long payTierId, long[] permissionIds) where TData : class, new();
    }
}
