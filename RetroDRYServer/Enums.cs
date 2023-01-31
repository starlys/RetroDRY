using System;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace RetroDRY
{
    /// <summary>
    /// Type of change for a single row, where "other" means it was edited but was not created or deleted
    /// </summary>
    public enum DiffKind { Other, NewRow, DeletedRow }

    [Flags]
    public enum PermissionLevel
    {
        None = 0,
        View = 1, 
        Modify = 2,
        Create = 4,
        Delete = 8,
        All = 15
    }
}
