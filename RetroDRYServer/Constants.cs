using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;

namespace RetroDRY
{
    /// <summary>
    /// Compile time constants
    /// </summary>
    public static class Constants
    {
        public static readonly JsonSerializerSettings CamelSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None
        };

        //error codes returned to http clients
        public const string 
            ERRCODE_INTERNAL = "INTERNAL", //internal error, unplanned for
            ERRCODE_BADUSER = "BADUSER", //unknown user
            ERRCODE_LOCK = "LOCKED", //cannot complete request because daton is locked by other user
            ERRCODE_VERSION = "VERSION" //cannot complete request due to out-of-date daton version
            ;

        //wire type names
        public const string TYPE_BOOL = "bool", TYPE_NBOOL = "nbool",
            TYPE_BYTE = "byte", TYPE_NBYTE = "nbyte",
            TYPE_INT16 = "int16", TYPE_NINT16 = "nint16",
            TYPE_INT32 = "int32", TYPE_NINT32 = "nint32",
            TYPE_INT64 = "int64", TYPE_NINT64 = "nint64",
            TYPE_DOUBLE = "double", TYPE_NDOUBLE = "ndouble",
            TYPE_DECIMAL = "decimal", TYPE_NDECIMAL = "ndecimal",
            TYPE_STRING = "string", TYPE_NSTRING = "nstring",
            TYPE_DATE = "date", TYPE_NDATE = "ndate",
            TYPE_DATETIME = "datetime", TYPE_NDATETIME = "ndatetime",
            TYPE_BLOB = "blob", TYPE_NBLOB = "nblob";

        /// <summary>
        /// wire type to CS type mapping, where the more preferred inference is listed first.
        /// Note same C# type is used in 2 wire types for string, dates, blob
        /// </summary>
        public static readonly (Type, string)[] TypeMap = new[]
        {
            (typeof(bool), TYPE_BOOL),
            (typeof(bool?), TYPE_NBOOL),
            (typeof(byte), TYPE_BYTE),
            (typeof(byte?), TYPE_NBYTE),
            (typeof(Int16), TYPE_INT16),
            (typeof(Int16?), TYPE_NINT16),
            (typeof(Int32), TYPE_INT32),
            (typeof(Int32?), TYPE_NINT32),
            (typeof(Int64), TYPE_INT64),
            (typeof(Int64?), TYPE_NINT64),
            (typeof(double), TYPE_DOUBLE),
            (typeof(double?), TYPE_NDOUBLE),
            (typeof(decimal), TYPE_DECIMAL),
            (typeof(decimal?), TYPE_NDECIMAL),
            (typeof(string), TYPE_STRING),
            (typeof(string), TYPE_NSTRING), 
            (typeof(DateTime), TYPE_DATE),
            (typeof(DateTime?), TYPE_NDATE),
            (typeof(DateTime), TYPE_DATETIME),
            (typeof(DateTime?), TYPE_NDATETIME),
            (typeof(byte[]), TYPE_BLOB),
            (typeof(byte[]), TYPE_NBLOB)
        };
    }
}
