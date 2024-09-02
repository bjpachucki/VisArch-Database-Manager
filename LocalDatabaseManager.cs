using Mono.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using VisArch.Data.DBTables;
using VisArch.Utilities;

namespace VisArch._StateMachines
{
    public class LocalDatabaseManager<T> : IDatabaseManager<T>
    {
        private string _connectionString;
        private string _tableName;

        public LocalDatabaseManager()
        {
            string databaseFilePath = Path.Combine(Application.streamingAssetsPath, "VisArch.db");
            _connectionString = $"Data Source={databaseFilePath};Version=3;";
        }

        public async Task<int> CountRowsAsync<TData>(TData data) where TData : class
        {
            if (string.IsNullOrEmpty(_tableName))
                _tableName = typeof(TData).Name;

            int rowCount = 0;

            var connection = new SqliteConnection(_connectionString);
            try
            {
                await connection.OpenAsync();

                var countCommand = connection.CreateCommand();
                countCommand.CommandText = $"SELECT COUNT(*) FROM {_tableName}";

                rowCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            }
            finally
            {
                connection.Close();
            }

            return rowCount;
        }

        public async Task DeleteWithJunctionsandRelatedAsync<TPrimary>(Tuple<string, object> primaryCondition, Type relatedTableType, params Type[] junctionTables) where TPrimary : class, new()
        {
            long? primaryId = await GetCurrentIdAsync<TPrimary>(primaryCondition);
            if (!primaryId.HasValue) throw new Exception($"No entry found for {primaryCondition.Item1} = {primaryCondition.Item2}");

            using(var connection = new SqliteConnection(_connectionString))
            {    
                await connection.OpenAsync();
                using(var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Delete from the main table
                        await DeleteRowAsync<TPrimary>(connection, transaction, primaryId.Value);

                        // 2. Delete from the related table
                        await DeleteRelatedRowAsync(connection, transaction, relatedTableType, primaryId.Value);

                        // 3. Delete from junction tables
                        foreach (var junctionTable in junctionTables)
                        {
                            await DeleteFromJunctionAsync<TPrimary>(connection, transaction, junctionTable, primaryId.Value);
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw; // rethrow the exception to handle it outside or let it bubble up
                    }
                }
            }
        }

        private async Task DeleteRowAsync<TPrimary>(SqliteConnection connection, SqliteTransaction transaction, long id) where TPrimary : class, new()
        {
            var tableName = typeof(T).Name;
            var primaryKey = ConditionBuilder.BuildPrimaryKeyCondition<TPrimary>();

            var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = $"DELETE FROM {tableName} WHERE {primaryKey} = @id";
            deleteCommand.Parameters.AddWithValue("@id", id);

            await deleteCommand.ExecuteNonQueryAsync();
        }

        private async Task DeleteRelatedRowAsync(SqliteConnection connection, SqliteTransaction transaction, Type relatedTableType, long primaryId)
        {
            var relatedTableName = relatedTableType.Name;
            var relatedTableIdName = ConditionBuilder.BuildPrimaryKeyCondition(relatedTableType);

            var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = $"DELETE FROM {relatedTableName} WHERE {relatedTableIdName} = @relatedId";
            deleteCommand.Parameters.AddWithValue("@relatedId", primaryId);

            await deleteCommand.ExecuteNonQueryAsync();
        }

        private async Task DeleteFromJunctionAsync<TPrimary>(SqliteConnection connection, SqliteTransaction transaction, Type junctionTable, long primaryId) where TPrimary : class, new()
        {
            var foreignKeyName = ConditionBuilder.BuildPrimaryKeyCondition<TPrimary>();

            var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = $"DELETE FROM {junctionTable.Name} WHERE {foreignKeyName} = @id";
            deleteCommand.Parameters.AddWithValue("@id", primaryId);

            await deleteCommand.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync<TData>(List<Tuple<string, object>> conditions) where TData : class
        {
            var connection = new SqliteConnection(_connectionString);
            try
            {
                await connection.OpenAsync();

                if (string.IsNullOrWhiteSpace(_tableName))
                    _tableName = typeof(TData).Name;

                // Building the WHERE clause from the conditions
                var whereClauses = conditions.Select(condition => $"{condition.Item1} = @{condition.Item1}").ToList();
                var whereClause = string.Join(" AND ", whereClauses);

                var command = connection.CreateCommand();
                command.CommandText = $"DELETE FROM {_tableName} WHERE {whereClause}";

                // Adding the parameters for the conditions
                foreach (var condition in conditions)
                {
                    command.Parameters.AddWithValue($"@{condition.Item1}", condition.Item2);
                }

                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                connection.Close();
            }
        }

        public async Task InsertAllAsync<TData>(IEnumerable<TData> data) where TData : class
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            if (string.IsNullOrWhiteSpace(_tableName))
                _tableName = typeof(TData).Name;

            var properties = typeof(TData).GetProperties();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    var columnNames = string.Join(", ", properties.Select(p => p.Name));
                    var parameterNames = string.Join(", ", properties.Select(p => $"@{p.Name}"));

                    command.CommandText = $"INSERT INTO {_tableName} ({columnNames}) VALUES ({parameterNames})";

                    foreach (var item in data)
                    {
                        foreach (var property in properties)
                        {
                            var value = property.GetValue(item);
                            command.Parameters.AddWithValue($"@{property.Name}", value ?? DBNull.Value);
                        }

                        await command.ExecuteNonQueryAsync();
                        command.Parameters.Clear();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public async Task InsertAllOrUpdateAsync<TData>(IEnumerable<TData> data) where TData : class
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            if (string.IsNullOrEmpty(_tableName))
                _tableName = typeof(TData).Name;

            PropertyInfo[] properties = typeof(TData).GetProperties();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    string columnNames = string.Join(", ", properties.Select(p => p.Name));
                    string parameterNames = string.Join(", ", properties.Select(p => $"@{p.Name}"));

                    // Get the property marked with ConflictResolutionColumnAttribute
                    var conflictResolutionProperty = properties.FirstOrDefault(p => p.GetCustomAttributes(typeof(UniqueDatabaseColumnAttribute), false).Any());
                    string conflictResolutionColumnName = conflictResolutionProperty != null
                        ? conflictResolutionProperty.GetCustomAttribute<UniqueDatabaseColumnAttribute>().ColumnName
                        : properties[0].Name;

                    string updateStatements = string.Join(", ", properties.Select(p => $"{p.Name} = EXCLUDED.{p.Name}"));

                    command.CommandText = $"INSERT INTO {_tableName} ({columnNames}) VALUES ({parameterNames}) " +
                                          $"ON CONFLICT({conflictResolutionColumnName}) DO UPDATE SET {updateStatements}";

                    foreach (PropertyInfo property in properties)
                    {
                        command.Parameters.Add(new SqliteParameter($"@{property.Name}", DbType.Object));
                    }

                    foreach (TData item in data)
                    {
                        foreach (PropertyInfo property in properties)
                        {
                            object value = property.GetValue(item);
                            command.Parameters[$"@{property.Name}"].Value = value ?? DBNull.Value;
                        }

                        await command.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public async Task InsertAsync(T data)
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            if (string.IsNullOrEmpty(_tableName))
                _tableName = data.GetType().Name;

            PropertyInfo[] properties = data.GetType().GetProperties();
            string columnNames = string.Join(", ", properties.Select(p => p.Name));
            string parameterNames = string.Join(", ", properties.Select(p => $"@{p.Name}"));

            var command = connection.CreateCommand();
            command.CommandText = $"INSERT INTO {_tableName} ({columnNames}) VALUES ({parameterNames})";

            foreach (PropertyInfo property in properties)
            {
                object value = property.GetValue(data);
                command.Parameters.AddWithValue($"@{property.Name}", value);
            }

            await command.ExecuteNonQueryAsync();
            connection.Close();
        }

        public async Task<long> InsertOrUpdateAsync(T data)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                if (string.IsNullOrEmpty(_tableName))
                    _tableName = data.GetType().Name;

                var properties = typeof(T).GetProperties();
                string columnNames = string.Join(", ", properties.Select(p => p.Name));
                string parameterNames = string.Join(", ", properties.Select(p => $"@{p.Name}"));

                var command = connection.CreateCommand();
                command.CommandText = $"INSERT OR REPLACE INTO {_tableName} ({columnNames}) VALUES ({parameterNames})";

                foreach (PropertyInfo property in properties)
                {
                    object value = property.GetValue(data);
                    command.Parameters.AddWithValue($"@{property.Name}", value);
                }

                await command.ExecuteNonQueryAsync();

                command.CommandText = "SELECT last_insert_rowid()";
                long lastRowId = (long)await command.ExecuteScalarAsync();

                return lastRowId;
            }
        }

        public async Task<long> UpdateSpecificRowAsync(T data, long id, string[] updateColumns)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                if (string.IsNullOrEmpty(_tableName))
                    _tableName = data.GetType().Name;

                // Filter the properties to those specified in updateColumns
                var properties = typeof(T).GetProperties().Where(prop => updateColumns.Contains(prop.Name));

                // Prepare the SET part of the SQL command
                string setClause = string.Join(", ", properties.Select(p => $"{p.Name} = @{p.Name}"));

                // Prepare the command text for the update operation
                var command = connection.CreateCommand();
                command.CommandText = $"UPDATE {_tableName} SET {setClause} WHERE resource_id = @Id";

                // Add parameters for the properties that are being updated
                foreach (var property in properties)
                {
                    var value = property.GetValue(data);
                    command.Parameters.AddWithValue($"@{property.Name}", value ?? DBNull.Value);
                }

                // Add the parameter for the ID
                command.Parameters.AddWithValue("@Id", id);

                // Execute the command and return the number of affected rows
                int affectedRows = await command.ExecuteNonQueryAsync();

                // In SQLite, last_insert_rowid() will return the rowid of the last row insert from the database connection
                // But since we are updating, we already know the id and just return it
                return id;
            }
        }

        public async Task UpdateAsync(T data)
        {
            var connection = new SqliteConnection(_connectionString);
            try
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = $"UPDATE {_tableName} SET ... WHERE ...";

                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                connection.Close();
            }
        }

        public async Task UpdateColumnAsync(string tableName, string columnName, object data, Tuple<string, object> condition)
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            object value; // the new value for the column

            switch(data.GetType())
            {
                case Type type when type == typeof(byte[]):
                    value = (byte[])data;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported data type: {data.GetType()}");
            }

            var command = connection.CreateCommand();
            command.CommandText = $"UPDATE {tableName} SET {columnName} = @value WHERE {condition.Item1} = @conditionValue";

            command.Parameters.AddWithValue("@value", value);
            command.Parameters.AddWithValue("@conditionValue", condition.Item2);

            await command.ExecuteNonQueryAsync();
            connection.Close();
        }

        public async Task<List<TData>> SelectAllAsync<TData>() where TData : class
        {
            List<TData> results = new List<TData>();

            if (string.IsNullOrEmpty(_tableName))
                _tableName = typeof(TData).Name;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM {_tableName}";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        TData item = Activator.CreateInstance<TData>();

                        foreach (var property in typeof(TData).GetProperties())
                        {
                            if (!reader.IsDBNull(reader.GetOrdinal(property.Name)))
                            {
                                var value = reader.GetValue(reader.GetOrdinal(property.Name));

                                if (property.PropertyType == typeof(byte[]) && value is byte[] blobValue)
                                {
                                    property.SetValue(item, blobValue);
                                }
                                else
                                {
                                    property.SetValue(item, value);
                                }
                            }
                        }

                        results.Add(item);
                    }
                }
            }

            return results;
        }

        public async Task UpdateJunctionForNewIdAsync<TSource, TJunction, TTarget>(long oldId, long newId, long targetId) where TSource : class, new() where TJunction : class, new() where TTarget : class, new()
        {
            string junctionTable = typeof(TJunction).Name;

            var foreignKeyMap = ConditionBuilder.BuildForeignKeyConditions<TJunction>();
            string junctionSourceLinkColumnName = foreignKeyMap[typeof(TSource).Name.ToLower()];
            string junctionTargetLinkColumnName = foreignKeyMap[typeof(TTarget).Name.ToLower()];

            // Update the junction in the database
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();

                command.CommandText = $@"
                UPDATE {junctionTable} 
                SET {junctionSourceLinkColumnName} = @newId 
                WHERE {junctionSourceLinkColumnName} = @oldId AND {junctionTargetLinkColumnName} = @targetId";

                command.Parameters.Add(new SqliteParameter("@oldId", oldId));
                command.Parameters.Add(new SqliteParameter("@newId", newId));
                command.Parameters.Add(new SqliteParameter("@targetId", targetId));

                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<TData>> SelectByPageAsync<TData>(int startIndex, int pageSize) where TData : class
        {
            List<TData> results = new List<TData>();

            if (string.IsNullOrEmpty(_tableName))
                _tableName = typeof(TData).Name;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM {_tableName} LIMIT @PageSize OFFSET @StartIndex";
                command.Parameters.AddWithValue("@PageSize", pageSize);
                command.Parameters.AddWithValue("@StartIndex", startIndex);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        TData item = Activator.CreateInstance<TData>();

                        foreach (var field in typeof(TData).GetFields())
                        {
                            if (!reader.IsDBNull(reader.GetOrdinal(field.Name)))
                            {
                                var value = reader.GetValue(reader.GetOrdinal(field.Name));

                                if (field.FieldType == typeof(byte[]) && value is byte[] blobValue)
                                {
                                    field.SetValue(item, blobValue);
                                }
                                else
                                {
                                    field.SetValue(item, value);
                                }
                            }
                        }

                        results.Add(item);
                    }
                }
            }

            return results;
        }

        public async Task<List<TData>> SelectBySingleConditionAsync<TData>(Tuple<string, object> condition) where TData : class, new()
        {
            List<TData> results = new List<TData>();

            if (string.IsNullOrEmpty(_tableName))
                _tableName = typeof(TData).Name;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM {_tableName} WHERE ";

                var parameters = new List<SqliteParameter>();
                bool isFirstCondition = true;

                foreach (var property in typeof(TData).GetProperties())
                {
                    if (condition.Item1 == property.Name)
                    {
                        var parameterName = $"@{property.Name}";
                        var parameterValue = condition.Item2;

                        if (!isFirstCondition)
                        {
                            command.CommandText += " AND ";
                        }
                        else
                        {
                            isFirstCondition = false;
                        }

                        command.CommandText += $"{property.Name} = {parameterName}";
                        parameters.Add(new SqliteParameter(parameterName, parameterValue));
                    }
                }

                command.Parameters.AddRange(parameters.ToArray());

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        TData item = Activator.CreateInstance<TData>();

                        foreach (var property in typeof(TData).GetProperties())
                        {
                            if (!reader.IsDBNull(reader.GetOrdinal(property.Name)))
                            {
                                var value = reader.GetValue(reader.GetOrdinal(property.Name));

                                if (property.PropertyType == typeof(byte[]) && value is byte[] blobValue)
                                {
                                    property.SetValue(item, blobValue);
                                }
                                else
                                {
                                    property.SetValue(item, value);
                                }
                            }
                        }

                        results.Add(item);
                    }
                }
            }

            return results;
        }

        public async Task<List<TData>> SelectByMultipleConditionsAsync<TData>(List<Tuple<string, object>> conditions) where TData : class, new()
        {
            List<TData> results = new List<TData>();

            if (string.IsNullOrEmpty(_tableName))
                _tableName = typeof(TData).Name;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM {_tableName} WHERE ";

                var parameters = new List<SqliteParameter>();
                bool isFirstCondition = true;

                foreach (var condition in conditions)
                {
                    foreach (var property in typeof(TData).GetProperties())
                    {
                        if (condition.Item1 == property.Name)
                        {
                            var parameterName = $"@{property.Name}";
                            var parameterValue = condition.Item2;

                            if (!isFirstCondition)
                            {
                                command.CommandText += " AND ";
                            }
                            else
                            {
                                isFirstCondition = false;
                            }

                            command.CommandText += $"{property.Name} = {parameterName}";
                            parameters.Add(new SqliteParameter(parameterName, parameterValue));
                        }
                    }
                }

                command.Parameters.AddRange(parameters.ToArray());

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        TData item = Activator.CreateInstance<TData>();

                        foreach (var property in typeof(TData).GetProperties())
                        {
                            if (!reader.IsDBNull(reader.GetOrdinal(property.Name)))
                            {
                                var value = reader.GetValue(reader.GetOrdinal(property.Name));

                                if (property.PropertyType == typeof(byte[]) && value is byte[] blobValue)
                                {
                                    property.SetValue(item, blobValue);
                                }
                                else
                                {
                                    property.SetValue(item, value);
                                }
                            }
                        }

                        results.Add(item);
                    }
                }
            }

            return results;
        }

        public async Task<List<TData>> SelectByIdsAsync<TData>(long[] ids) where TData : class, new()
        {
            List<TData> results = new List<TData>();

            if (string.IsNullOrEmpty(_tableName))
                _tableName = typeof(TData).Name;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                var idsParameter = string.Join(", ", ids.Select(id => $"@id{id}"));
                string primaryKeyColumnName = ConditionBuilder.BuildPrimaryKeyCondition<TData>();

                foreach (var id in ids)
                {
                    command.Parameters.AddWithValue($"@id{id}", id);
                }
                command.CommandText = $"SELECT * FROM {_tableName} WHERE {primaryKeyColumnName} IN ({idsParameter})";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        TData item = Activator.CreateInstance<TData>();

                        foreach (var property in typeof(TData).GetProperties())
                        {
                            if (!reader.IsDBNull(reader.GetOrdinal(property.Name)))
                            {
                                var value = reader.GetValue(reader.GetOrdinal(property.Name));
                                if (property.PropertyType == typeof(byte[]) && value is byte[] blobValue)
                                {
                                    property.SetValue(item, blobValue);
                                }
                                else
                                {
                                    property.SetValue(item, value);
                                }
                            }
                        }

                        results.Add(item);
                    }
                }
            }

            return results;
        }

        public async Task<List<TTarget>> SelectByJunctionConditionAsync<TSource, TJunction, TTarget>(Tuple<string, object> sourceCondition)
            where TSource : class, new()
            where TJunction : class, new()
            where TTarget : class, new()
        {
            List<TTarget> results = new List<TTarget>();

            string sourceTable = typeof(TSource).Name;
            string junctionTable = typeof(TJunction).Name;
            string targetTable = typeof(TTarget).Name;

            string sourceLinkColumnName = ConditionBuilder.BuildPrimaryKeyCondition<TSource>();
            var foreignKeyMap = ConditionBuilder.BuildForeignKeyConditions<TJunction>();

            string junctionSourceLinkColumnName = foreignKeyMap[typeof(TSource).Name.ToLower()];
            string junctionTargetLinkColumnName = foreignKeyMap[typeof(TTarget).Name.ToLower()];
            string targetLinkColumnName = ConditionBuilder.BuildPrimaryKeyCondition<TTarget>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();

                command.CommandText = $@"
                SELECT t.* 
                FROM {targetTable} t
                JOIN {junctionTable} j ON t.{targetLinkColumnName} = j.{junctionTargetLinkColumnName}
                JOIN {sourceTable} s ON s.{sourceLinkColumnName} = j.{junctionSourceLinkColumnName}
                WHERE s.{sourceCondition.Item1} = @{sourceCondition.Item1}";

                command.Parameters.Add(new SqliteParameter($"@{sourceCondition.Item1}", sourceCondition.Item2));

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        TTarget item = Activator.CreateInstance<TTarget>();
                        foreach (var property in typeof(TTarget).GetProperties())
                        {
                            if (!reader.IsDBNull(reader.GetOrdinal(property.Name)))
                            {
                                var value = reader.GetValue(reader.GetOrdinal(property.Name));
                                property.SetValue(item, value);
                            }
                        }
                        results.Add(item);
                    }
                }
            }

            return results;
        }

        public async Task CreateJunctionForNewIdAsync<TSource, TJunction, TTarget>(long sourceId, long targetId)
    where TSource : class, new()
    where TJunction : class, new()
    where TTarget : class, new()
        {
            string junctionTable = typeof(TJunction).Name;

            var foreignKeyMap = ConditionBuilder.BuildForeignKeyConditions<TJunction>();
            string junctionSourceLinkColumnName = foreignKeyMap[typeof(TSource).Name.ToLower()];
            string junctionTargetLinkColumnName = foreignKeyMap[typeof(TTarget).Name.ToLower()];

            // Construct the junction object to insert
            TJunction newJunction = Activator.CreateInstance<TJunction>();
            var sourceProperty = typeof(TJunction).GetProperty(junctionSourceLinkColumnName);
            var targetProperty = typeof(TJunction).GetProperty(junctionTargetLinkColumnName);

            if (sourceProperty != null && targetProperty != null)
            {
                sourceProperty.SetValue(newJunction, sourceId);
                targetProperty.SetValue(newJunction, targetId);
            }

            // Insert the junction into the database
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();

                command.CommandText = $@"
            INSERT INTO {junctionTable} ({junctionSourceLinkColumnName}, {junctionTargetLinkColumnName}) 
            VALUES (@sourceId, @targetId)";

                command.Parameters.Add(new SqliteParameter("@sourceId", sourceId));
                command.Parameters.Add(new SqliteParameter("@targetId", targetId));

                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<bool> IsValueUniqueAsync<TData>(string columnName, TData value) where TData : IComparable<TData>
        {
            if (string.IsNullOrEmpty(_tableName))
                _tableName = typeof(TData).Name;

            var connection = new SqliteConnection(_connectionString);
            try
            {
                await connection.OpenAsync();

                var uniqueCommand = connection.CreateCommand();
                uniqueCommand.CommandText = $"SELECT COUNT(*) FROM {_tableName} WHERE {columnName} = @value";
                uniqueCommand.Parameters.AddWithValue("@value", value);

                int count = Convert.ToInt32(await uniqueCommand.ExecuteScalarAsync());

                return count == 0;
            }
            finally
            {
                connection.Close();
            }
        }

        public async Task<long?> GetCurrentIdAsync<TData>(Tuple<string, object> condition) where TData : class, new()
        {
            if (string.IsNullOrEmpty(_tableName))
                _tableName = typeof(TData).Name;

            // Get the primary key column name for the given TData
            string primaryKeyColumnName = ConditionBuilder.BuildPrimaryKeyCondition<TData>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var queryCommand = connection.CreateCommand();

                // Use the primary key column name instead of the hardcoded 'id'
                queryCommand.CommandText = $"SELECT {primaryKeyColumnName} FROM {_tableName} WHERE {condition.Item1} = @value";

                queryCommand.Parameters.AddWithValue("@value", condition.Item2);

                var result = await queryCommand.ExecuteScalarAsync();

                // If no match is found, ExecuteScalar will return null
                if (result != null && result is long id)
                    return id;
                else
                    return null; // or handle as you see fit
            }
        }

        public async Task<long> GetNextIdAsync<TData>(string columnName) where TData : class
        {
            if (string.IsNullOrEmpty(_tableName))
                _tableName = typeof(TData).Name;

            long nextId = 0;

            var connection = new SqliteConnection(_connectionString);
            try
            {
                await connection.OpenAsync();

                var maxCommand = connection.CreateCommand();
                maxCommand.CommandText = $"SELECT MAX({columnName}) FROM {_tableName}";

                var result = await maxCommand.ExecuteScalarAsync();
                if (result != DBNull.Value && result is long)
                {
                    nextId = (long)result + 1;
                }
                else
                {
                    nextId = 1;
                }
            }
            finally
            {
                connection.Close();
            }

            return nextId;
        }

        public async Task<List<TData>> SelectByPayTierIdAndPermissionIdsAsync<TData>(long payTierId, long[] permissionIds) where TData : class, new()
        {
            List<TData> results = new List<TData>();

            if (string.IsNullOrEmpty(_tableName))
                _tableName = typeof(TData).Name;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM {_tableName} WHERE pay_tier_id = @payTierId AND permission_id IN ({string.Join(",", permissionIds)})";

                command.Parameters.AddWithValue("@payTierId", payTierId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        TData item = Activator.CreateInstance<TData>();

                        foreach (var property in typeof(TData).GetProperties())
                        {
                            if (!reader.IsDBNull(reader.GetOrdinal(property.Name)))
                            {
                                var value = reader.GetValue(reader.GetOrdinal(property.Name));

                                if (property.PropertyType == typeof(byte[]) && value is byte[] blobValue)
                                {
                                    property.SetValue(item, blobValue);
                                }
                                else
                                {
                                    property.SetValue(item, value);
                                }
                            }
                        }

                        results.Add(item);
                    }
                }
            }

            return results;
        }
    }
}
