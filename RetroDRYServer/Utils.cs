using System;
using System.Collections;
using System.Data;
using System.Linq;
using System.Reflection;

#pragma warning disable IDE0019

namespace RetroDRY
{
    /// <summary>
    /// Miscellaneous utility functions
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// True if the type is one of the supported numeric types
        /// </summary>
        public static bool IsSupportedNumericType(Type t)
        {
            return t == typeof(byte) || t == typeof(Int16) || t == typeof(Int32) || t == typeof(Int64)
                || t == typeof(double) || t == typeof(decimal)
                || t == typeof(byte?) || t == typeof(Int16?) || t == typeof(Int32?) || t == typeof(Int64?)
                || t == typeof(double?) || t == typeof(decimal?);
        }

        /// <summary>
        /// True if the type can be used as a column
        /// </summary>
        public static bool IsSupportedType(Type t)
        {
            return t == typeof(string) || t == typeof(DateTime) || t == typeof(DateTime?) || t == typeof(byte[]) 
                || t == typeof(bool) || t == typeof(bool?) || IsSupportedNumericType(t);
        }

        /// <summary>
        /// Get the inferred wire type for this C# type, or throw exception
        /// </summary>
        public static string InferredWireType(Type t)
        {
            foreach (var pair in Constants.TypeMap)
                if (pair.Item1 == t) return pair.Item2;
            throw new Exception($"Type {t.Name} is not supported");
        }

        /// <summary>
        /// Construct an instance of type t, using parameterless constructor
        /// </summary>
        public static object Construct(Type t)
        {
            var ctor = t.GetConstructor(new Type[0]);
            if (ctor == null) throw new Exception($"{t.Name} must have a parameterless constructor");
            return ctor.Invoke(new object[0]);
        }

        /// <summary>
        /// Construct a row object and set any custom non-nullable properties to their defaults
        /// </summary>
        /// <param name="tabledef">if null, this behaves like Construct(t)</param>
        /// <param name="t">type to construct</param>
        public static Row ConstructRow(Type t, TableDef? tabledef) 
        {
            if (!typeof(Row).IsAssignableFrom(t)) throw new Exception("Requires Row type");
            var row = Construct(t) as Row;
            if (row == null) throw new Exception("Could not construct row");
            if (tabledef != null)
            {
                foreach (var coldef in tabledef.Cols.Where(c => c.IsCustom))
                    if (coldef.CSType.IsValueType && Nullable.GetUnderlyingType(coldef.CSType) == null)
                        row.SetCustom(coldef.Name, Activator.CreateInstance(coldef.CSType));
            }
            return row;
        }

        /// <summary>
        /// Construct a daton object using ConstructRow if it is a single main row style daton, else just construct the type
        /// </summary>
        public static Daton ConstructDaton(Type t, DatonDef datondef)
        {
            var d = ConstructRow(t, datondef.MultipleMainRows ? null : datondef.MainTableDef) as Daton;
            if (d == null) throw new Exception("Could not construct daton");
            return d;
        }

        /// <summary>
        /// Change the value of certain defaults to be more user friendly (namely, non-nullable dates and times)
        /// </summary>
        /// <param name="d">a newly created persiston</param>
        /// <param name="datondef">definition of d</param>
        public static void FixTopLevelDefaultsInNewPersiston(DatonDef datondef, Daton d)
        {
            if (datondef.MultipleMainRows || datondef.MainTableDef == null) return;
            foreach (var coldef in datondef.MainTableDef.Cols)
                if (coldef.CSType == typeof(DateTime))
                {
                    var field = datondef.Type.GetField(coldef.Name);
                    field.SetValue(d, DateTime.UtcNow);
                }
        }

        /// <summary>
        /// Wrapper to Convert.ChangeType that allows for nullable types, treating empty strings as null
        /// </summary>
        public static object? ChangeType(object? value, Type type)
        {
            if (value == null || (value is string vs && vs.Length == 0))
            {
                if (type.IsValueType) return Activator.CreateInstance(type);
                return null;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return Convert.ChangeType(value, type.GetGenericArguments()[0]);
            return Convert.ChangeType(value, type);
        }

        /// <summary>
        /// Given an object o, get the value of field; but if it is null, then construct an instance of the field
        /// type and set it. The generic type T can be identical to the known field type, or an ancestor.
        /// The use case is for daton rows with member List of child rows, to get the child list using IList as the type T.
        /// </summary>
        public static T? CreateOrGetFieldValue<T>(object o, FieldInfo field) where T:class
        {
            T? v = field.GetValue(o) as T;
            if (v == null)
            {
                v = Construct(field.FieldType) as T;
                field.SetValue(o, v);
            }
            return v;
        }

        /// <summary>
        /// Find the element in the targetList whose value of pkField is pk, and return the index, or -1 if not found
        /// </summary>
        /// <param name="pkField">must be a field defined within the type of the elements of targetList</param>
        /// <param name="pk">value to look for</param>
        /// <param name="targetList">list to look in</param>
        public static int IndexOfPrimaryKeyMatch(IList targetList, FieldInfo pkField, object? pk)
        {
            int idx = -1;
            foreach (var target in targetList)
            {
                ++idx;
                var pk2 = pkField.GetValue(target);
                if (pk2 != null && pk2.Equals(pk)) return idx;
            }
            return -1;
        }

        /// <summary>
        /// Add a parameter and value to a DbCommand
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public static void AddParameterWithValue(IDbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }

        /// <summary>
        /// DataReader read string value with DBNull conversion to null
        /// </summary>
        /// <param name="rdr"></param>
        /// <param name="idx"></param>
        /// <returns></returns>
        public static string? ReadString(IDataReader rdr, int idx)
        {
            object value = rdr.GetValue(idx);
            if (value is DBNull) return null;
            return (string)value;
        }

        /// <summary>
        /// DataReader read value with DBNull conversion to default
        /// </summary>
        public static T ReadPrimitive<T>(IDataReader rdr, int idx) where T: struct 
        {
            object value = rdr.GetValue(idx);
            if (value is DBNull) return default;
            return (T)value;
        }
    }
}
