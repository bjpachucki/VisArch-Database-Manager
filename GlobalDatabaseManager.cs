using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace VisArch._StateMachines
{
    public class GlobalDatabaseManager : MonoBehaviour
    {
        public enum DatabaseMode
        {
            local,
            cloud
        }

        private static GlobalDatabaseManager _instance;

        public static GlobalDatabaseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject obj = new GameObject("GlobalDatabaseManager");
                    _instance = obj.AddComponent<GlobalDatabaseManager>();
                }
                return _instance;
            }
        }

        private bool _useLocalDatabase = true;

        public DatabaseMode DbMode;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize(DbMode);                
        }

        public void Initialize(DatabaseMode dbMode)
        {
            DbMode = dbMode;

            switch (DbMode)
            {
                case DatabaseMode.local:
                    {
                        _useLocalDatabase = true;
                    }
                    break;
                case DatabaseMode.cloud:
                    {
                        _useLocalDatabase = false;
                    }
                    break;
                default:
                    break;
            }
        }

        public Task<int> CountRowsAsync<TData>(TData data = default) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.CountRowsAsync(data);
        }

        public Task DeleteAsync<TData>(List<Tuple<string, object>> conditions) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.DeleteAsync<TData>(conditions);
        }

        public Task InsertAllAsync<TData>(IEnumerable<TData> data) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.InsertAllAsync(data);
        }

        public Task InsertAllOrUpdateAsync<TData>(IEnumerable<TData> data) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.InsertAllOrUpdateAsync(data);
        }

        public Task InsertAsync<TData>(TData payload) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.InsertAsync(payload);
        }

        public Task<long> InsertOrUpdateAsync<TData>(TData payload) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.InsertOrUpdateAsync(payload);
        }

        public Task<long> UpdateSpecificRowAsync<TData>(TData payload, long id, string[] updateColumns) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.UpdateSpecificRowAsync(payload, id, updateColumns);
        }

        public Task<List<TData>> SelectAllAsync<TData>() where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.SelectAllAsync<TData>();
        }

        public Task<List<TData>> SelectByPageAsync<TData>(int startIndex, int pageSize) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.SelectByPageAsync<TData>(startIndex, pageSize);
        }

        public Task<List<TData>> SelectBySingleConditionAsync<TData>(Tuple<string, object> condition) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.SelectBySingleConditionAsync<TData>(condition);
        }

        public Task<List<TData>> SelectMultipleConditionsAsync<TData>(List<Tuple<string, object>> conditions) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.SelectByMultipleConditionsAsync<TData>(conditions);
        }

        public Task UpdateColumnAsync(string tableName, string columnName, object data, Tuple<string, object> condition)
        {
            DatabaseAccessManager<object> dbInstance = CreateDatabaseInstance<object>();
            return dbInstance.UpdateColumnAsync(tableName, columnName, data, condition);
        }

        public async Task UpdateJunctionForNewIdAsync<TSource, TJunction, TTarget>(long oldId, long newId, long targetId) where TSource : class, new() where TJunction : class, new() where TTarget : class, new()
        {
            DatabaseAccessManager<TSource> dbInstance = CreateDatabaseInstance<TSource>();
            await dbInstance.UpdateJunctionForNewIdAsync<TSource,TJunction,TTarget>(oldId, newId, targetId);
        }

        public Task<List<TData>> SelectByIdsAsync<TData>(long[] ids) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.SelectByIdsAsync<TData>(ids);
        }

        public Task<List<TTarget>> SelectByJunctionConditionAsync<TSource, TJunction, TTarget>(Tuple<string, object> sourceCondition)
            where TSource : class, new()
            where TJunction : class, new()
            where TTarget : class, new()
        {
            DatabaseAccessManager<TTarget> dbInstance = CreateDatabaseInstance<TTarget>();
            return dbInstance.SelectByJunctionConditionAsync<TSource, TJunction, TTarget>(sourceCondition);
        }

        public async Task UpdatePrimaryTableAndJunctionsAsync<TPrimary>(TPrimary data, params Type[] junctionTableTypes) where TPrimary : class, new()
        {
            //We need to get the Id first so we don't need oldResourceID
            var propertyValue = typeof(TPrimary).GetProperty("name").GetValue(data);

            var selector = ConditionBuilder.BuildPropertySelector<TPrimary>("name");
            var condition = ConditionBuilder.BuildSingleCondition(selector, propertyValue);

            long? oldIdNullable = await GetCurrentIdAsync<TPrimary>(condition);

            // Check if oldIdNullable has a value
            if (!oldIdNullable.HasValue)
            {
                throw new InvalidOperationException("Old ID is null and cannot be converted to a non-nullable long.");
            }

            long oldId = oldIdNullable.Value;
            long newID = await InsertOrUpdateAsync(data);

            var updatedJunctionDataList =  ConditionBuilder.CreateJunctionDataListWithUpdatedId<TPrimary>(oldId, newID, junctionTableTypes);

            foreach (var junctionData in updatedJunctionDataList)
            {
                var method = GetType().GetMethod("InsertOrUpdateAsync");
                var generic = method.MakeGenericMethod(junctionData.GetType());
                await (Task)generic.Invoke(this, new object[] { junctionData });
            }
        }

        public async Task DeleteWithJunctionsAndRelatedAsync<TPrimary>(Tuple<string, object> primaryCondition, Type relatedTableType, params Type[] junctionTables) where TPrimary : class, new()
        {
            DatabaseAccessManager<TPrimary> dbInstance = CreateDatabaseInstance<TPrimary>();
            await dbInstance.DeleteWithJunctionsandRelatedAsync<TPrimary>(primaryCondition, relatedTableType, junctionTables);
        }

        public async Task CreateJunctionForNewIdAsync<TSource, TJunction, TTarget>(long sourceId, long targetId) where TSource : class, new() where TJunction : class, new() where TTarget : class, new()
        {
            DatabaseAccessManager<TJunction> dbInstance = CreateDatabaseInstance<TJunction>();
            await dbInstance.CreateJunctionForNewIdAsync<TSource, TJunction, TTarget>(sourceId, targetId);
        }

        public Task<long?> GetCurrentIdAsync<TData>(Tuple<string, object> condition) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.GetCurrentIdAsync<TData>(condition);
        }

        public Task<bool> IsValueUniqueAsync<TData>(string columnName, TData value) where TData : class, IComparable<TData>, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return dbInstance.IsValueUniqueAsync(columnName, value);
        }

        public async Task<long> GetNextIdAsync<TData>(string columnName) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return await dbInstance.GetNextIdAsync<TData>(columnName);
        }

        public async Task<List<TData>> SelectByPayTierIdAndPermissionIdsAsync<TData>(long payTierId, long[] permissionIds) where TData : class, new()
        {
            DatabaseAccessManager<TData> dbInstance = CreateDatabaseInstance<TData>();
            return await dbInstance.SelectByPayTierIdAndPermissionIdsAsync<TData>(payTierId, permissionIds);
        }

        private DatabaseAccessManager<TData> CreateDatabaseInstance<TData>() where TData : class, new()
        {
            DatabaseAccessManager<TData> databaseContext = new DatabaseAccessManager<TData>(_useLocalDatabase);
            return databaseContext;
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class DBManagerInitializer
    {
        static DBManagerInitializer()
        {
            EditorApplication.delayCall += InitializeDBManager;
        }

        private static void InitializeDBManager()
        {
            // Try to find an existing GlobalDatabaseManager in the scene
            GlobalDatabaseManager existingInstance = GameObject.FindObjectOfType<GlobalDatabaseManager>();

            // If found, initialize it, otherwise do nothing
            if (existingInstance != null)
            {
                existingInstance.Initialize(GlobalDatabaseManager.DatabaseMode.local);
            }
        }
    }
#endif
}
