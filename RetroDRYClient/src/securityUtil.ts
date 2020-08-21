import {ColDefResponse, TableDefResponse, DatonDefResponse} from './wireTypes';

//static object containing constants and utility functions for handling permissions
const securityUtil = {

    //permission levels corresponding to c# PermissionLevel enum
    level_none: 0,
    level_view: 1, 
    level_modify: 2,
    level_create: 4,
    level_delete: 8,
    level_all: 15,

    //convenience functions to interpret permissionLevel ints
    canEditPersiston: (datonDef: DatonDefResponse): boolean => (datonDef.mainTableDef.permissionLevel & securityUtil.level_modify) !== 0,
    canDeletePersiston: (datonDef: DatonDefResponse): boolean => (datonDef.mainTableDef.permissionLevel & securityUtil.level_delete) !== 0,
    canViewColDef: (colDef: ColDefResponse): boolean  => (colDef.permissionLevel & securityUtil.level_view) !== 0,
    canEditColDef: (colDef: ColDefResponse): boolean  => (colDef.permissionLevel & securityUtil.level_modify) !== 0,
    canEditColDefInNewRow: (colDef: ColDefResponse): boolean  => (colDef.permissionLevel & securityUtil.level_create) !== 0,
    canCreateRow: (tableDef: TableDefResponse): boolean  => (tableDef.permissionLevel & securityUtil.level_create) !== 0,
    canDeleteRow: (tableDef: TableDefResponse): boolean  => (tableDef.permissionLevel & securityUtil.level_delete) !== 0,

    //mark row as created client side (this affects permissions since any created row is editable)
    markRowCreatedOnClient: (row: any) => row['$new'] = true,

    //get whether row created client side
    isRowCreatedOnClient: (row: any): boolean => row['$new'] === true
};

export {securityUtil};