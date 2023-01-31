using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RetroDRY
{
    /// <summary>
    /// Base class for requests from client to server
    /// </summary>
    public abstract class RetroRequest
    {
        /// <summary>
        /// Session initiating request
        /// </summary>
        public string? SessionKey { get; set; }

        /// <summary>
        /// Arbitrary string provided by client and can be used to route among multiple retroverses
        /// </summary>
        public string? Environment { get; set; }
    }

    /// <summary>
    /// A long-polling request that has no payload, for the purpose of receiving information back in a server-push style
    /// </summary>
    public class LongRequest : RetroRequest
    {
    }

    /// <summary>
    /// The main request, which encapsulates all RetryDRY client request types
    /// </summary>
    public class MainRequest : RetroRequest
    {
        /// <summary>
        /// When present, this is a request to initialize the connection
        /// </summary>
        public InitializeRequest? Initialize { get; set; }

        /// <summary>
        /// When present, this is a request to get one or more datons by key
        /// </summary>
        public GetDatonRequest[]? GetDatons { get; set; }

        /// <summary>
        /// When present, this is a request to change the state of one or more datons
        /// </summary>
        public ManageDatonRequest[]? ManageDatons { get; set; }

        /// <summary>
        /// When present, this is a request to save one or more datons; see specification for format
        /// </summary>
        public JObject[]? SaveDatons { get; set; }

        /// <summary>
        /// When true, this is a request to end the session
        /// </summary>
        public bool DoQuit { get; set; }
    }

    /// <summary>
    /// Request to initialize connection
    /// </summary>
    public class InitializeRequest
    {
        /// <summary>
        /// language code as defined by containing app
        /// </summary>
        public string? LanguageCode { get; set; }
    }

    /// <summary>
    /// Request to get one daton
    /// </summary>
    public class GetDatonRequest
    {
        /// <summary>
        /// string version of DatonKey
        /// </summary>
        public string? Key { get; set; }
        
        /// <summary>
        /// If true, client will subscribe to changes in this daton
        /// </summary>
        public bool DoSubscribe { get; set; }

        /// <summary>
        /// If true, server will load from database, even if cached
        /// </summary>
        public bool ForceLoad { get; set; }

        /// <summary>
        /// If set,.. (documentation needed)
        /// </summary>
        public string? KnownVersion { get; set; }
    }

    /// <summary>
    /// Request to change the subscribe state of one daton
    /// </summary>
    public class ManageDatonRequest
    {
        /// <summary>
        /// string version of DatonKey
        /// </summary>
        public string? Key { get; set; }

        /// <summary>
        /// 0=unsubscribed, 1=subscribed, 2=subscribed and locked
        /// </summary>
        public int SubscribeState { get; set; }

        /// <summary>
        /// required version that the client has
        /// </summary>
        public string? Version { get; set; }
    }

    /// <summary>
    /// Base class for server reponses to client
    /// </summary>
    public abstract class RetroResponse
    {
        /// <summary>
        /// If non-null, the error cide
        /// </summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Response to a client-initiated request
    /// </summary>
    public class MainResponse : RetroResponse
    {
        /// <summary>
        /// Null or the data dictionary that the client should use for the duration of the session
        /// </summary>
        public DataDictionaryResponse? DataDictionary { get; set; }

        /// <summary>
        /// Null or the datons requested
        /// </summary>
        public GetDatonResponse[]? GetDatons { get; set; }

        /// <summary>
        /// Null or the new state of the datons (used when the client requested a change of state)
        /// </summary>
        public ManageDatonResponse[]? ManageDatons { get; set; }

        /// <summary>
        /// Array corresponding to the requested persistons to save; it will be in the same order, but in the case of errors,
        /// it will only contain up through the errored member
        /// </summary>
        public SavePersistonResponse[]? SavedPersistons { get; set; }

        /// <summary>
        /// True when a save was successful
        /// </summary>
        public bool SavePersistonsSuccess { get; set; }
    }

    /// <summary>
    /// Response to a long-polling request, which contains info pushed by the server side
    /// </summary>
    public class LongResponse : RetroResponse
    {
        /// <summary>
        /// The new data dictionary to replace the one already known by the client
        /// </summary>
        public DataDictionaryResponse? DataDictionary { get; set; }

        /// <summary>
        /// New daton values; this will be set when the client is subscribed and the server finds that a daton has changed
        /// </summary>
        public CondensedDatonResponse[]? CondensedDatons { get; set; }
    }

    /// <summary>
    /// Container for the whole data dictionary
    /// </summary>
    public class DataDictionaryResponse
    {
        /// <summary>
        /// Collection of daton definitions
        /// </summary>
        public List<DatonDefResponse>? DatonDefs { get; set; }

        /// <summary>
        /// Natural language messages whose keys match those desclared in Constants.EnglishMessages, in the language of this session
        /// </summary>
        public Dictionary<string, string>? MessageConstants { get; set; }
    }

    /// <summary>
    /// Daton definition
    /// </summary>
    public class DatonDefResponse
    {
        /// <summary>
        /// Type name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// True if persiston; false if viewon
        /// </summary>
        public bool IsPersiston { get; set; }

        /// <summary>
        /// Definition of daton's main table
        /// </summary>
        public TableDefResponse? MainTableDef { get; set; }

        /// <summary>
        /// For viewons, this may be set to a quasi-table whose columns define the criteria. Otherwise null.
        /// </summary>
        public TableDefResponse? CriteriaDef { get; set; }

        /// <summary>
        /// If true, the daton subclass should contain only one or more Lists of rows using nested types; if false,
        /// the daton subclass should declare the main row fields at the top level
        /// </summary>
        public bool MultipleMainRows { get; set; }
    }

    /// <summary>
    /// Wire-formatted table definition
    /// </summary>
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class TableDefResponse
    {
        /// <summary>
        /// Table name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Level for this user; values based on PermissionLevel enum
        /// </summary>
        public int PermissionLevel { get; set; }

        /// <summary>
        /// Columns in table that map to fields in a Row object
        /// </summary>
        public List<ColDefResponse>? Cols { get; set; }

        /// <summary>
        /// Child tables 
        /// </summary>
        public List<TableDefResponse?>? Children { get; set; } 

        /// <summary>
        /// The column name of the primary key in this table
        /// </summary>
        public string? PrimaryKeyColName { get; set; }

        /// <summary>
        /// Table prompt in natural language 
        /// </summary>
        public string? Prompt { get; set; }

        /// <summary>
        /// True if this "table" is actually the collection of criteria definitions
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsCriteria { get; set; }
    }

    /// <summary>
    /// Wire-formatted column definition
    /// </summary>
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class ColDefResponse
    {
        /// <summary>
        /// column name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Level for this user; values based on PermissionLevel enum
        /// </summary>
        public int PermissionLevel { get; set; }

        /// <summary>
        /// type name (such as bool, string, int32, byte[], datetime, etc
        /// </summary>
        public string? WireType { get; set; }

        /// <summary>
        /// If true, the column won't be editable by users (either because it's a primary key or because it's not in the storage table)
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsComputed { get; set; }

        /// <summary>
        /// If true, this column can be used to sort
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool AllowSort { get; set; }

        /// <summary>
        /// The daton type that this column references
        /// </summary>
        public string? ForeignKeyDatonTypeName { get; set; }

        /// <summary>
        /// Information needed to define selection behavior
        /// </summary>
        public ColDef.SelectBehaviorInfo? SelectBehavior;

        /// <summary>
        /// Information needed to load this column via a left-join.
        /// </summary>
        public ColDef.LeftJoinInfo? LeftJoin;

        /// <summary>
        /// If true, this column is the readable name or description that users would see to identify the row (or in some cases it could also be the primary key)
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsMainColumn { get; set; }

        /// <summary>
        /// If true, this column will be shown in dropdowns along with the one marked with IsMainColumn, when selecting rows
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsVisibleInDropdown { get; set; }

        /// <summary>
        /// Column prompt in natural language 
        /// </summary>
        public string? Prompt { get; set; }

        /// <summary>
        /// String minimum length
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int MinLength { get; set; }

        /// <summary>
        /// String maximum length (or 0 for unlimited)
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int MaxLength { get; set; }

        /// <summary>
        /// Validation message with optional placeholders: {0}=prompt {1}=maxlength {2}=minlength
        /// </summary>
        public string? LengthValidationMessage { get; set; } 

        /// <summary>
        /// Validation regular expression
        /// </summary>
        public string? Regex { get; set; }

        /// <summary>
        /// Validation message with optional placeholders: {0}=prompt
        /// </summary>
        public string? RegexValidationMessage { get; set; }

        /// <summary>
        /// The smallest number allowed
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal MinNumberValue { get; set; }

        /// <summary>
        /// The largest number allowed
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal MaxNumberValue { get; set; }

        /// <summary>
        /// Validation message with optional placeholders: {0}=prompt {1}=min {2}=max
        /// </summary>
        public string? RangeValidationMessage { get; set; }

        /// <summary>
        /// Set only if this column is an image identifier (such as a file name) 
        /// The column name in the same row whose value will be set to the URL of the image
        /// </summary>
        public string? ImageUrlColumName { get; set; }
    }

    /// <summary>
    /// Either CondensedDaton is set here, or Key and Errors are set (key is NOT set on success)
    /// </summary>
    public class GetDatonResponse
    {
        /// <summary>
        /// The JSON of a condensed daton - see wire specification
        /// </summary>
        public CondensedDatonResponse? CondensedDaton { get; set; }

        /// <summary>
        /// Only set if daton is not returned in CondensedDaton so the client can correlate the errors; null if there are no errors and CondensedDaton is set
        /// </summary>
        public string? Key { get; set; }

        /// <summary>
        /// Any user readable errors
        /// </summary>
        public string[]? Errors { get; set; }
    }

    /// <summary>
    /// Container of JSON for a condensed daton
    /// </summary>
    [JsonConverter(typeof(Retrovert.CondensedDatonResponseConverter))]
    public class CondensedDatonResponse
    {
        /// <summary>
        /// The overridden JSON serializer ensures this gets sent out as JSON, not as a string
        /// </summary>
        public string? CondensedDatonJson { get; set; }
    }

    /// <summary>
    /// Response for a request to change a daton state
    /// </summary>
    public class ManageDatonResponse
    {
        /// <summary>
        /// string form of DatonKey
        /// </summary>
        public string? Key { get; set; }

        /// <summary>
        /// new subscription state (see ManageDatonRequest)
        /// </summary>
        public int SubscribeState { get; set; }
        
        /// <summary>
        /// If nonnull, error code
        /// </summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Response for a request to save a persiston
    /// </summary>
    public class SavePersistonResponse
    {
        /// <summary>
        /// The original key (which may indicate an unpersisted daton)
        /// </summary>
        public string? OldKey { get; set; }

        /// <summary>
        /// This key will be different from the requested key for new persistons
        /// </summary>
        public string? NewKey { get; set; }

        /// <summary>
        /// New daton version, or null if error
        /// </summary>
        public string? NewVersion { get; set; }

        /// <summary>
        /// Any errors
        /// </summary>
        public string[]? Errors { get; set; }
        
        /// <summary>
        /// True if save was successful
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// True if entire persison was deleted
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
