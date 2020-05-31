//Classes documented in C# server tier

export interface RetroRequest
{
    sessionKey: string;
}

export interface LongRequest extends RetroRequest
{
}

export interface MainRequest extends RetroRequest
{
    initialze?: InitializeRequest;
    getDatons?: GetDatonRequest[];
    manageDatons?: ManageDatonRequest[];
    saveDatons?: SaveDatonRequest[];
    doQuit?: boolean;
}

export interface InitializeRequest
{
    languageCode: string;
}

export interface GetDatonRequest
{
    key: string;
    doSubscribe: boolean;
    forceLoad: boolean;
    knownVersion?: string|null;
}

export interface ManageDatonRequest
{
    key: string;
    subscribeState: 0|1|2;
    version: string;
}

export interface SaveDatonRequest
{
    diff: any;
}

export interface RetroResponse
{
    errorCode?: string;
}

export interface MainResponse extends RetroResponse
{
    dataDictionary?: DataDictionaryResponse;
    permissionSet?: PermissionResponse ;
    condensedDatons?: CondensedDatonResponse[];
    manageDatons?: ManageDatonResponse[];
    savedPersistons?: SavePersistonResponse[];
    savePersistonsSuccess?: boolean;
}

export interface LongResponse extends RetroResponse
{
    permissionSet?: PermissionResponse;
    condensedDatons?: CondensedDatonResponse[];
}

export interface DataDictionaryResponse
{
    datonDefs: DatonDefResponse[];
}

export interface DatonDefResponse
{
    name: string;
    mainTableDef: TableDefResponse;
    criteriaDef?: TableDefResponse;
    multipleMainRows: boolean;
}

export interface TableDefResponse
{
    name: string;
    cols: ColDefResponse[];
    children?: TableDefResponse[]; 
    primaryKeyColName?: string;
    prompt?: string;
}

export interface ColDefResponse
{
    name: string;
    wireType: string;
    isComputed: boolean; 
    allowSort: boolean;
    foreignKeyDatonTypeName: string;
    isMainColumn: boolean;
    isVisibleInDropdown: boolean;
    prompt?: string;
    minLength: number;
    maxLength: number;
    lengthValidationMessage?: string; 
    regex?: string;
    regexValidationMessage?: string;
    minNumberValue: number;
    maxNumberValue: number;
    rangeValidationMessage?: string;
    imageUrlColumName?: string;
}

export interface PermissionResponse
{
    level: number;
    overrides: DetailPermisionResponse[];
}

export interface DetailPermisionResponse
{
    name: string;
    level: number;
    overrides: DetailPermisionResponse[];
}

export interface CondensedDatonResponse
{
    isComplete?: boolean;
    condensedDatonJson: string;
}

export interface ManageDatonResponse
{
    key: string;
    subscribeState: number;
    errorCode?: string;
}

export interface SavePersistonResponse
{
    oldKey: string;
    newKey: string;
    errors?: string[];
    isSuccess: boolean;
    isDeleted: boolean;
}
