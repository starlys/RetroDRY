﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RetroDRY
{
    public class RetroRequest
    {
        public string SessionKey { get; set; }
    }

    public class LongRequest : RetroRequest
    {
    }

    public class MainRequest : RetroRequest
    {
        public InitializeRequest Initialze { get; set; }
        public GetDatonRequest[] GetDatons { get; set; }
        public ManageDatonRequest[] ManageDatons { get; set; }
        public JObject[] SaveDatons { get; set; }
        public bool DoQuit { get; set; }
    }

    public class InitializeRequest
    {
        public string LanguageCode { get; set; }
    }

    public class GetDatonRequest
    {
        public string Key { get; set; }
        public bool DoSubscribe { get; set; }
        public bool ForceLoad { get; set; }
        public string KnownVersion { get; set; }
    }

    public class ManageDatonRequest
    {
        public string Key { get; set; }

        /// <summary>
        /// 0=unsubscribed, 1=subscribed, 2=subscribed and locked
        /// </summary>
        public int SubscribeState { get; set; }

        public string Version { get; set; }
    }

    public class RetroResponse
    {
        public string ErrorCode { get; set; }
    }

    public class MainResponse : RetroResponse
    {
        public DataDictionaryResponse DataDictionary { get; set; }
        public PermissionResponse PermissionSet { get; set; }
        public CondensedDatonResponse[] CondensedDatons { get; set; }
        public ManageDatonResponse[] ManageDatons { get; set; }

        /// <summary>
        /// Array corresponding to the requested persistons to save; it will be in the same order, but in the case of errors,
        /// it will only contain up through the errored member
        /// </summary>
        public SavePersistonResponse[] SavedPersistons { get; set; }
        public bool SavePersistonsSuccess { get; set; }
    }

    public class LongResponse : RetroResponse
    {
        public PermissionResponse PermissionSet { get; set; }
        public CondensedDatonResponse[] CondensedDatons { get; set; }
    }

    public class DataDictionaryResponse
    {
        public List<DatonDefResponse> DatonDefs { get; set; }
    }

    public class DatonDefResponse
    {
        public string Name { get; set; }

        public bool IsPersiston { get; set; }

        public TableDefResponse MainTableDef { get; set; }

        /// <summary>
        /// For viewons, this may be set to a quasi-table whose columns define the criteria. Otherwise null.
        /// </summary>
        public TableDefResponse CriteriaDef { get; set; }

        /// <summary>
        /// If true, the daton subclass should contain only one or more Lists of rows using nested types; if false,
        /// the daton subclass should declare the main row fields at the top level
        /// </summary>
        public bool MultipleMainRows { get; set; }
    }

    public class TableDefResponse
    {
        public string Name { get; set; }

        /// <summary>
        /// Columns in table that map to fields in a Row object
        /// </summary>
        public List<ColDefResponse> Cols { get; set; }

        /// <summary>
        /// Child tables 
        /// </summary>
        public List<TableDefResponse> Children { get; set; } 

        /// <summary>
        /// The column name of the primary key in this table
        /// </summary>
        public string PrimaryKeyColName { get; set; }

        /// <summary>
        /// Table prompt in natural language 
        /// </summary>
        public string Prompt { get; set; }

        public bool IsCriteria { get; set; }
    }

    public class ColDefResponse
    {
        /// <summary>
        /// column name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// type name (such as bool, string, int32, byte[], datetime, etc
        /// </summary>
        public string WireType { get; set; }

        /// <summary>
        /// If true, the column won't be editable on clients
        /// </summary>
        public bool IsComputed { get; set; }

        /// <summary>
        /// If true, this column can be used to sort
        /// </summary>
        public bool AllowSort { get; set; }

        /// <summary>
        /// The daton type that this column references
        /// </summary>
        public string ForeignKeyDatonTypeName { get; set; }

        /// <summary>
        /// When this column is a left-joined description column, the foreign key column in this same table
        /// </summary>
        public string LeftJoinForeignKeyColumnName;

        /// <summary>
        /// When this column is a left-joined description column, the column in the joined table to join in (usually a name or description column)
        /// </summary>
        public string LeftJoinRemoteDisplayColumnName;

        /// <summary>
        /// The viewon type name used to choose values for this column
        /// </summary>
        public string LookupViewonTypeName;

        /// <summary>
        /// Used with LookupViewonTypeName; refers to the key column in the viewon whose value should be copied into this column
        /// </summary>
        public string LookupViewonKeyColumnName;

        /// <summary>
        /// If true, this column is the readable name or description that users would see to identify the row (or in some cases it could also be the primary key)
        /// </summary>
        public bool IsMainColumn { get; set; }

        /// <summary>
        /// If true, this column will be shown in dropdowns along with the one marked with IsMainColumn, when selecting rows
        /// </summary>
        public bool IsVisibleInDropdown { get; set; }

        /// <summary>
        /// Column prompt in natural language 
        /// </summary>
        public string Prompt { get; set; }

        /// <summary>
        /// String minimum length
        /// </summary>
        public int MinLength { get; set; }

        /// <summary>
        /// String maximum length (or 0 for unlimited)
        /// </summary>
        public int MaxLength { get; set; }

        /// <summary>
        /// Validation message with optional placeholders: {0}=prompt {1}=maxlength {2}=minlength
        /// </summary>
        public string LengthValidationMessage { get; set; } 

        public string Regex { get; set; }

        /// <summary>
        /// Validation message with optional placeholders: {0}=prompt
        /// </summary>
        public string RegexValidationMessage { get; set; }

        public decimal MinNumberValue { get; set; }

        public decimal MaxNumberValue { get; set; }

        /// <summary>
        /// Validation message with optional placeholders: {0}=prompt {1}=min {2}=max
        /// </summary>
        public string RangeValidationMessage { get; set; }

        /// <summary>
        /// Set only if this column is an image identifier (such as a file name) { get; set; } 
        /// The column name in the same row whose value will be set to the URL of the image
        /// </summary>
        public string ImageUrlColumName { get; set; }
    }

    public class PermissionResponse
    {
        /// <summary>
        /// also see DetailPermissionResponse; this is the base level for all datons/tables
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// table overrides
        /// </summary>
        public List<DetailPermisionResponse> Overrides;
    }

    /// <summary>
    /// Class serves as table and column level in the response
    /// </summary>
    public class DetailPermisionResponse
    {
        /// <summary>
        /// The name of the table or column
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// defined by the enum PermissionLevel, which can be ORd together
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// For tables, this lists the column overrides (not used for columns)
        /// </summary>
        public List<DetailPermisionResponse> Overrides;
    }

    [JsonConverter(typeof(Retrovert.CondensedDatonResponseConverter))]
    public class CondensedDatonResponse
    {
        /// <summary>
        /// The overridden JSON serializer ensures this gets sent out as JSON with property name "daton", not as a string
        /// </summary>
        public string CondensedDatonJson { get; set; }
    }

    public class ManageDatonResponse
    {
        public string Key { get; set; }

        /// <summary>
        /// new subscription state (see ManageDatonRequest)
        /// </summary>
        public int SubscribeState { get; set; }
        
        public string ErrorCode { get; set; }
    }

    public class SavePersistonResponse
    {
        public string OldKey { get; set; }

        /// <summary>
        /// This key will be different from the requested key for new persistons
        /// </summary>
        public string NewKey { get; set; }

        public string[] Errors { get; set; }
        
        public bool IsSuccess { get; set; }

        /// <summary>
        /// True if entire persison was deleted
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
