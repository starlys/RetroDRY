using System;
using System.Collections;
using System.Data;
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
                || IsSupportedNumericType(t);
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
        /// Given an object o, get the value of field; but if it is null, then construct an instance of the field
        /// type and set it. The generic type T can be identical to the known field type, or an ancestor.
        /// The use case is for daton rows with member List of child rows, to get the child list using IList as the type T.
        /// </summary>
        public static T CreateOrGetFieldValue<T>(object o, FieldInfo field) where T:class
        {
            T v = field.GetValue(o) as T;
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
        public static int IndexOfPrimaryKeyMatch(IList targetList, FieldInfo pkField, object pk)
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

        public static void AddParameterWithValue(IDbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
    }
}
