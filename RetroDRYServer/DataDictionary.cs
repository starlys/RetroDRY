using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace RetroDRY
{
    public class DataDictionary
    {
        //these lists are populated during AddDatonUsingClassAnnotation (they can't be filled any other way) and then cleared out in Finalize
        private List<(TableDef, InheritFromAttribute)> TableInheritances = new List<(TableDef, InheritFromAttribute)>();
        private List<(ColDef, InheritFromAttribute)> ColumnInheritances = new List<(ColDef, InheritFromAttribute)>();
        private List<ColDef> IncompleteSelectBehaviors = new List<ColDef>();

        /// <summary>
        /// daton definitions indexed by name
        /// </summary>
        public Dictionary<string, DatonDef> DatonDefs = new Dictionary<string, DatonDef>();

        public bool IsFinalized { get; private set; }

        /// <summary>
        /// Alternative to DatonDefs[d.Key.Name] with clearer error reporting
        /// </summary>
        public DatonDef FindDef(Daton d) => FindDef(d.Key.Name);

        /// <summary>
        /// Alternative to DatonDefs[key.name] with clearer error reporting
        /// </summary>
        public DatonDef FindDef(DatonKey key) => FindDef(key.Name);

        /// <summary>
        /// Alternative to DatonDefs[name] with clearer error reporting
        /// </summary>
        public DatonDef FindDef(string name)
        {
            if (DatonDefs.TryGetValue(name, out var datondef)) return datondef;
            throw new Exception($"The daton type '{name}' is not registered.");
        }

        /// <summary>
        /// Call AddDatonUsingClassAnnotation for all Daton subclasses in the given assembly
        /// </summary>
        public void AddDatonsUsingClassAnnotation(Assembly assembly)
        {
            //doing it in order of Persistons then Viewons will catch some two-step inheritances, which are not strictly supported
            var types = assembly.GetTypes();
            foreach (var type in types.Where(t => typeof(Persiston).IsAssignableFrom(t)))
                AddDatonUsingClassAnnotation(type);
            foreach (var type in types.Where(t => typeof(Viewon).IsAssignableFrom(t)))
                AddDatonUsingClassAnnotation(type);
        }

        /// <summary>
        /// Add a daton definition based on analyzing the attributes applied to the given class
        /// </summary>
        /// <returns>Can return null if the type should not be added</returns>
        public DatonDef AddDatonUsingClassAnnotation(Type datonType)
        {
            //validate
            if (!typeof(Daton).IsAssignableFrom(datonType)) throw new Exception("Can only add subclasses of Daton");
            if (datonType.GetCustomAttribute<RetroHideAttribute>() != null) return null;

            //add to collection
            var datondef = new DatonDef { Type = datonType };
            DatonDefs[datonType.Name] = datondef;
            bool isViewon = typeof(Viewon).IsAssignableFrom(datondef.Type);

            //look for annotations that can only be applied to daton classes
            var databaseNumber = datonType.GetCustomAttribute<DatabaseNumberAttribute>();
            if (databaseNumber != null) datondef.DatabaseNumber = databaseNumber.Num;
            datondef.MultipleMainRows = datonType.GetCustomAttribute<SingleMainRowAttribute>() == null;

            //look for annotations on the single main row and recur
            if (!datondef.MultipleMainRows)
            {
                datondef.MainTableDef = new TableDef { Name = datonType.Name, RowType = datonType };
                PopulateTableAnnotations(isViewon, datonType, datondef.MainTableDef);
            }

            //if no single main row, look for field annotations on the daton to find the main table
            else
                PopulateFieldAnnotations(isViewon, datondef, datonType, null, true);

            //build data dict for criteria
            if (isViewon) PopulateCriteria(datondef, datonType);

            return datondef;
        }

        /// <summary>
        /// finalize the inheritance by copying table and column definitions from their sources, based on the InheritFrom annotation.
        /// Caller should do all the manual data dictionary setup before calling this.
        /// </summary>
        public void FinalizeInheritance()
        {
            if (TableInheritances == null) throw new Exception("Cannot call FinalizeInhertiance twice");

            //copy inheritance at table level (including col names that match)
            foreach ((var targetTabledef, var inherit) in TableInheritances)
            {
                (var sourceTabledef, var _) = FindInheritanceSource(inherit.SourceName, false);
                if (targetTabledef.ParentKeyColumnName == null)
                    targetTabledef.ParentKeyColumnName = sourceTabledef.ParentKeyColumnName;
                if (targetTabledef.PrimaryKeyColName == null)
                    targetTabledef.PrimaryKeyColName = sourceTabledef.PrimaryKeyColName;
                if (targetTabledef.Prompt == null)
                    targetTabledef.Prompt = sourceTabledef.Prompt;
                if (targetTabledef.SqlTableName == null)
                    targetTabledef.SqlTableName = sourceTabledef.SqlTableName;
                foreach (var targetColdef in targetTabledef.Cols)
                {
                    var sourceColdef = sourceTabledef.FindCol(targetColdef.Name);
                    if (sourceColdef == null || sourceColdef.IsCustom) continue;
                    CopyColumnInheritance(sourceColdef, targetColdef);
                }

                //include custom cols
                if (inherit.IncludeCustom)
                {
                    foreach (var sourceColdef in sourceTabledef.Cols.Where(c => c.IsCustom))
                    {
                        var targetColdef = targetTabledef.AddCustomColum(sourceColdef.Name, sourceColdef.CSType, sourceColdef.WireType);
                        CopyColumnInheritance(sourceColdef, targetColdef);
                    }
                }
            }

            //copy inheritance at column level
            foreach ((var targetColdef, var inherit) in ColumnInheritances)
            {
                (var sourceTabledef, var sourceColdef) = FindInheritanceSource(inherit.SourceName, true);
                CopyColumnInheritance(sourceColdef, targetColdef);
            }

            //fix any assumed defaults in lookup behavior
            foreach (var coldef in IncompleteSelectBehaviors)
            {
                if (DatonDefs.TryGetValue(coldef.SelectBehavior.ViewonTypeName, out var viewonDef))
                {
                    coldef.SelectBehavior.ViewonValueColumnName = viewonDef.MainTableDef?.PrimaryKeyColName;
                }
            }

            TableInheritances = null;
            ColumnInheritances = null;
            IncompleteSelectBehaviors = null;
            IsFinalized = true;
        }

        /// <summary>
        /// Throw exception if anything in the data dictionary is not supported
        /// </summary>
        public void Validate()
        {
            foreach (var datondef in DatonDefs.Values)
            {
                datondef.MainTableDef.Validate(false);
                datondef.CriteriaDef?.Validate(true);
            }
        }

        private static void CopyColumnInheritance(ColDef sourceColdef, ColDef targetColdef)
        {
            //inherit storage, entry and display related properties
            targetColdef.IsCustom = sourceColdef.IsCustom;
            if (targetColdef.Image == null) targetColdef.Image = sourceColdef.Image; //don't overwrite
            targetColdef.IsComputed = sourceColdef.IsComputed;
            targetColdef.IsMainColumn |= sourceColdef.IsMainColumn; //true if defined or inherited
            targetColdef.IsVisibleInDropdown |= sourceColdef.IsVisibleInDropdown; //true if defined or inherited

            //inherit validation related properties that make sense for viewon criteria.
            //Example: if a persiston column requires 2-5 characters, then the corresponding criterion should allow 0-5 characters
            //So, this omits MinLength, Regex
            targetColdef.MaxLength = sourceColdef.MaxLength; 
            targetColdef.LengthValidationMessage = sourceColdef.LengthValidationMessage;
            targetColdef.MinNumberValue = sourceColdef.MinNumberValue;
            targetColdef.MaxNumberValue = sourceColdef.MaxNumberValue;
            targetColdef.RangeValidationMessage = sourceColdef.RangeValidationMessage;

            //inherit other UI things
            if (targetColdef.Prompt == null) targetColdef.Prompt = sourceColdef.Prompt; //don't overwrite

            //omit per rule in guide saying db load properties are not inherited: 
            //targetColdef.ForeignKeyDatonTypeName = sourceColdef.ForeignKeyDatonTypeName;
        }

        /// <summary>
        /// Look for attributes on a Row class and set tabldef properties; then call PopulateFieldAnnotations to recur to all children of this table
        /// </summary>
        private void PopulateTableAnnotations(bool isViewon, Type rowType, TableDef tabledef) 
        {
            var customSqlName = rowType.GetCustomAttribute<SqlTableNameAttribute>();
            if (customSqlName != null) tabledef.SqlTableName = customSqlName.Name;
            else tabledef.SqlTableName = tabledef.Name;
          
            var prompt = rowType.GetCustomAttribute<PromptAttribute>();
            if (prompt != null) tabledef.Prompt = DataDictionary.SetPrompt(tabledef.Prompt, prompt.Prompt);

            var parentKey = rowType.GetCustomAttribute<ParentKeyAttribute>();
            if (parentKey != null) tabledef.ParentKeyColumnName = parentKey.ColumnName;

            var inherit = rowType.GetCustomAttribute<InheritFromAttribute>();
            if (inherit != null) TableInheritances.Add((tabledef, inherit));

            PopulateFieldAnnotations(isViewon, null, rowType, tabledef, false);
        }

        /// <summary>
        /// Loop columns defined in a Row class, then look for attributes set on those fields, and set coldef properties.
        /// Also find List members and recur for child tables.
        /// </summary>
        /// <param name="datondef">set for top level only, else null</param>
        /// <param name="isTopLevel">true only when the daton class itself is the type argument</param>
        /// <param name="tabledef">null if top level</param>
        private void PopulateFieldAnnotations(bool isViewon, DatonDef datondef, Type type, TableDef tabledef, bool isTopLevel)
        {
            foreach (var field in type.GetFields())
            {
                if (field.GetCustomAttribute<RetroHideAttribute>() != null) continue;

                if (typeof(IList).IsAssignableFrom(field.FieldType))
                {
                    //found child list
                    if (field.FieldType.GenericTypeArguments.Length != 1) throw new Exception($"Child table {field.Name} must be defined as List<T>");
                    var childTabledef = new TableDef { Name = field.Name, RowType = field.FieldType.GenericTypeArguments[0] };
                    if (!typeof(Row).IsAssignableFrom(childTabledef.RowType)) throw new Exception($"Child table {field.Name} must have a generic argument that is a subclass of Row");
                    if (isTopLevel)
                    {
                        //this is the main table in a multi-main-row daton
                        if (datondef.MainTableDef != null) throw new Exception($"Only one member of daton {type.Name} can be a List, because only one main table is supported");
                        tabledef = childTabledef;
                        datondef.MainTableDef = childTabledef;
                        PopulateTableAnnotations(isViewon, tabledef.RowType, tabledef);
                    }
                    else
                    {
                        //this is an actual child table
                        if (tabledef.Children == null) tabledef.Children = new List<TableDef>();
                        tabledef.Children.Add(childTabledef);
                        PopulateTableAnnotations(isViewon, childTabledef.RowType, childTabledef);

                        //validate
                        if (childTabledef.ParentKeyColumnName == null) throw new Exception($"ParentKey attribute missing from child table {field.Name}");
                    }
                }
                else
                {
                    //found child column
                    if (isTopLevel) throw new Exception($"Class {type.Name} may not have fields other than the List for the main table; use SingleMainRow on the daton class if you meant to define a single main row");
                    if (!Utils.IsSupportedType(field.FieldType)) throw new Exception($"{type.Name}.{field.Name} is not one of the supported types");
                    var coldef = new ColDef { Name = field.Name, CSType = field.FieldType };
                    tabledef.Cols.Add(coldef);

                    var wiretype = field.GetCustomAttribute<WireTypeAttribute>();
                    if (wiretype != null)
                        coldef.WireType = wiretype.TypeName;
                    else
                        coldef.WireType = Utils.InferredWireType(coldef.CSType);

                    var key = field.GetCustomAttribute<PrimaryKeyAttribute>();
                    if (key != null)
                    {
                        if (tabledef.PrimaryKeyColName != null) throw new Exception($"PrimaryKey attribute may not be used on more than one field member of {tabledef.Name}");
                        tabledef.PrimaryKeyColName = field.Name;
                        tabledef.DatabaseAssignsKey = key.DatabaseAssigned;
                    }
                    
                    var stringlength = field.GetCustomAttribute<StringLengthAttribute>();
                    if (stringlength != null)
                    {
                        coldef.MinLength = stringlength.MinimumLength;
                        coldef.MaxLength = stringlength.MaximumLength;
                        coldef.LengthValidationMessage = SetPrompt(coldef.LengthValidationMessage, stringlength.ErrorMessage);
                    }
                    
                    var regex = field.GetCustomAttribute<RegularExpressionAttribute>();
                    if (regex != null)
                    {
                        coldef.Regex = regex.Pattern;
                        coldef.RegexValidationMessage = SetPrompt(coldef.RegexValidationMessage, regex.ErrorMessage);
                    }
                    
                    var range = field.GetCustomAttribute<RangeAttribute>();
                    if (range != null)
                    {
                        try
                        {
                            coldef.MinNumberValue = Convert.ToDecimal(range.Minimum);
                            coldef.MaxNumberValue = Convert.ToDecimal(range.Maximum);
                            coldef.RangeValidationMessage = SetPrompt(coldef.RangeValidationMessage, range.ErrorMessage);
                        }
                        catch
                        {
                            throw new Exception($"Range validation on {coldef.Name} must use integer or decimal values");
                        }
                    }
                    
                    var foreignkey = field.GetCustomAttribute<ForeignKeyAttribute>();
                    if (foreignkey != null) coldef.ForeignKeyDatonTypeName = foreignkey.Target.Name;

                    var selectBehavior = field.GetCustomAttribute<SelectBehaviorAttribute>();
                    if (selectBehavior != null)
                    {
                        coldef.SelectBehavior = new ColDef.SelectBehaviorInfo
                        {
                            ViewonTypeName = selectBehavior.ViewonType.Name,
                            UseDropdown = selectBehavior.UseDropdown,
                            ViewonValueColumnName = selectBehavior.ViewonValueColumnName,
                            AutoCriterionName = selectBehavior.AutoCriterionName,
                            AutoCriterionValueColumnName = selectBehavior.AutoCriterionValueColumnName
                        };
                        if (coldef.SelectBehavior.ViewonValueColumnName == null) IncompleteSelectBehaviors.Add(coldef); //to be fixed during finalization
                    }

                    var prompt = field.GetCustomAttribute<PromptAttribute>();
                    if (prompt != null) coldef.Prompt = SetPrompt(coldef.Prompt, prompt.Prompt); 
                    
                    var maincol = field.GetCustomAttribute<MainColumnAttribute>();
                    if (maincol != null) coldef.IsMainColumn = true;
                    
                    var visibledropdown = field.GetCustomAttribute<VisibleInDropdownAttribute>();
                    if (visibledropdown != null) coldef.IsVisibleInDropdown = true;
                    
                    var image = field.GetCustomAttribute<ImageColumnAttribute>();
                    if (image != null) coldef.Image = new ColDef.ImageInfo { UrlColumName = image.UrlColumnName };
                    
                    var computed = field.GetCustomAttribute<ComputedColumnAttribute>();
                    if (computed != null) coldef.IsComputed = true;

                    var sort = field.GetCustomAttribute<SortColumnAttribute>();
                    if (sort != null)
                    {
                        coldef.AllowSort = true;
                        if (sort.IsDefault) tabledef.DefaulSortColName = coldef.Name;
                    }

                    var leftjoin = field.GetCustomAttribute<LeftJoinAttribute>();
                    if (leftjoin != null)
                    {
                        coldef.LeftJoin = new ColDef.LeftJoinInfo
                        {
                            ForeignKeyColumnName = leftjoin.ForeignKeyColumnName,
                            RemoteDisplayColumnName = leftjoin.DisplayColumnName
                        };
                    }

                    var inherit = field.GetCustomAttribute<InheritFromAttribute>();
                    if (inherit != null) ColumnInheritances.Add((coldef, inherit));
                }
            }

            //validate and fix
            bool hasChildTables = tabledef.Children != null && tabledef.Children.Count > 0;
            bool canOmitPK = isViewon && !hasChildTables;
            if (!canOmitPK && tabledef.PrimaryKeyColName == null) 
                throw new Exception($"PrimaryKey attribute must be set on a field member of {tabledef.Name}");
            if (tabledef.DefaulSortColName == null) 
                tabledef.DefaulSortColName = tabledef.PrimaryKeyColName;
        }

        /// <summary>
        /// unpack a source name as defined by InheritFromAttribute and return the table and optionally column identified by the name;
        /// for example "Customer.Name" in column mode
        /// </summary>
        /// <param name="sourceName">dot-delimited names starting with daton name and optionally including table names, and optionally ending with column name</param>
        /// <param name="forColumn">if true, the last part of sourceName must be a column and ColDef will be returned; if false, no column name will be on sourceName, and ColDef will be null in the return pair.</param>
        private (TableDef, ColDef) FindInheritanceSource(string sourceName, bool forColumn)
        {
            //parse the string into the names for identifying the daton, tables and column
            var parts = sourceName.Split('.');
            string datonName = parts[0];
            string[] tableNames;
            string colName = null;
            if (forColumn)
            {
                tableNames = parts.Skip(1).Take(parts.Length - 2).ToArray();
                colName = parts[parts.Length - 1];
            }
            else tableNames = parts.Skip(1).ToArray();

            //find the objects
            if (!DatonDefs.TryGetValue(datonName, out DatonDef datondef)) throw new Exception($"InheritFrom syntax '{sourceName}' does not refer to any known daton type");
            var tabledef = datondef.MainTableDef;
            foreach (string tableName in tableNames)
            {
                tabledef = tabledef.FindChildTable(tableName);
                if (tabledef == null) throw new Exception($"InheritFrom syntax '{sourceName}' does not refer to any known table name; {tableName} is unknown");
            }
            ColDef coldef = null;
            if (forColumn)
            {
                coldef = tabledef.FindCol(colName);
                if (coldef == null) throw new Exception($"InheritFrom syntax '{sourceName}' does not refer to any known column name; {colName} is unknown");
            }

            return (tabledef, coldef);
        }

        /// <summary>
        /// Examine the datonType for a directly nested class annotated with Criteria, and use its field members
        /// to build the data dictionary of criteria
        /// </summary>
        private void PopulateCriteria(DatonDef datondef, Type datonType)
        {
            //get the criteria class
            var ctypes = datonType.GetNestedTypes().Where(t => t.GetCustomAttribute<CriteriaAttribute>() != null);
            if (ctypes.Count() == 0) return;
            if (ctypes.Count() > 1) throw new Exception("Only one class within a daton may be annotated with Criteria: " + datonType.Name);
            var ctype = ctypes.First();

            //store quasi-table to contain the crieria definitions, and check for inheritance
            datondef.CriteriaDef = new TableDef { Name = ctype.Name, RowType = ctype };
            var inheritAll = ctype.GetCustomAttribute<InheritFromAttribute>();
            if (inheritAll != null) TableInheritances.Add((datondef.CriteriaDef, inheritAll));
            PopulateTableAnnotations(true, ctype, datondef.CriteriaDef);
        }

        /// <summary>
        /// Given a prompt structure or null, return the same instance or a new one with the default-language prompt set to the given prompt
        /// </summary>
        internal static SortedList<string, string> SetPrompt(SortedList<string, string> dict, string prompt)
        {
            if (dict == null) dict = new SortedList<string, string>(1);
            dict[""] = prompt;
            return dict;
        }

        /// <summary>
        /// Given a prompt in a SortedList structure (as defined in various places)
        /// </summary>
        public static string ResolvePrompt(SortedList<string, string> prompt, IUser user, string defaultValue = "")
        {
            if (prompt == null) return defaultValue;
            prompt.TryGetValue(user.LangCode ?? "", out string s);
            return s ?? defaultValue;
        }
    }
}
