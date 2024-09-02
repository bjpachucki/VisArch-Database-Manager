using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using VisArch.Data.DBTables;

namespace VisArch._StateMachines
{
    public static class ConditionBuilder
    {
        public static Tuple<string, object> BuildSingleCondition<TData, TValue>(Expression<Func<TData, TValue>> selector, TValue value)
        {
            MemberExpression memberExpression = null;

            // Check if the body is a MemberExpression
            if (selector.Body is MemberExpression)
            {
                memberExpression = (MemberExpression)selector.Body;
            }
            // Check if the body is a UnaryExpression (like a Convert node)
            else if (selector.Body is UnaryExpression unaryExpression && unaryExpression.NodeType == ExpressionType.Convert)
            {
                memberExpression = unaryExpression.Operand as MemberExpression;
            }

            // If the expression couldn't be resolved, throw an exception
            if (memberExpression == null)
            {
                throw new InvalidOperationException("The provided selector expression is not supported.");
            }

            var propertyName = memberExpression.Member.Name;
            return new Tuple<string, object>(propertyName, value);
        }

        public static string BuildPrimaryKeyCondition<TData>() where TData : class, new()
        {
            var type = typeof(TData);

            var primaryKeyAttribute = type.GetCustomAttributes(typeof(PrimaryKeyAttribute), false).FirstOrDefault() as PrimaryKeyAttribute;

            if (primaryKeyAttribute == null)
            {
                throw new InvalidOperationException($"No primary key specified for entity type: {type.Name}");
            }

            return primaryKeyAttribute.ColumnName;
        }

        public static string BuildPrimaryKeyCondition(Type type)
        {
            var primaryKeyAttribute = type.GetCustomAttributes(typeof(PrimaryKeyAttribute), false).FirstOrDefault() as PrimaryKeyAttribute;

            if (primaryKeyAttribute == null)
            {
                throw new InvalidOperationException($"No primary key specified for entity type: {type.Name}");
            }

            return primaryKeyAttribute.ColumnName;
        }

        public static Expression<Func<T, object>> BuildPropertySelector<T>(string propertyName)
        {
            var parameter = Expression.Parameter(typeof(T), "entity");
            var propertyAccess = Expression.PropertyOrField(parameter, propertyName);
            var convert = Expression.Convert(propertyAccess, typeof(object));
            return Expression.Lambda<Func<T, object>>(convert, parameter);
        }

        public static Dictionary<string, string> BuildForeignKeyConditions<TJunction>() where TJunction : class, new()
        {
            // This will hold the detected foreign key properties for the junction table
            Dictionary<string, string> foreignKeys = new Dictionary<string, string>();

            foreach (var prop in typeof(TJunction).GetProperties())
            {
                // Check if property name ends with "_id" and isn't just "id"
                if (prop.Name.EndsWith("_id") && prop.Name != "id")
                {
                    string relatedTableName = prop.Name.Substring(0, prop.Name.Length - 3); // removes "_id"
                    foreignKeys[relatedTableName] = prop.Name;
                }
            }

            return foreignKeys;
        }

        public static List<object> CreateJunctionDataListWithUpdatedId<TPrimary>(long oldId, long newId, params Type[] junctionTypes) where TPrimary : class, new()
        {
            List<object> updatedJunctionDataList = new List<object>();

            foreach (var type in junctionTypes)
            {
                if (!type.IsClass)
                    throw new ArgumentException($"Type {type.Name} is not a class.");

                var junctionObject = Activator.CreateInstance(type);

                var method = typeof(ConditionBuilder).GetMethod("BuildForeignKeyConditions");
                var generic = method.MakeGenericMethod(type);
                var foreignKeys = (Dictionary<string, string>)generic.Invoke(null, null);

                foreach (var key in foreignKeys.Values)
                {
                    var prop = type.GetProperty(key);

                    if(prop.GetValue(junctionObject).Equals(oldId) )
                    {
                        prop.SetValue(junctionObject, newId);
                        updatedJunctionDataList.Add(junctionObject);
                    }
                }
            }

            return updatedJunctionDataList;
        }
    }
}
