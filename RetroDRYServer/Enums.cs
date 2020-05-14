using System;

namespace RetroDRY
{
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
