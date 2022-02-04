//Classes documented in C# server tier

export interface RetroRequest
{
    sessionKey: string;
    environment: string;
}

export interface LongRequest extends RetroRequest
{
}

export interface MainRequest extends RetroRequest
{
    initialize?: InitializeRequest;
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
    getDatons?: GetDatonResponse[]; 
    manageDatons?: ManageDatonResponse[];
    savedPersistons?: SavePersistonResponse[];
    savePersistonsSuccess?: boolean;
}

export interface LongResponse extends RetroResponse
{
    dataDictionary?: DataDictionaryResponse;
    condensedDatons?: CondensedDatonResponse[];
}

export interface DataDictionaryResponse
{
    datonDefs: DatonDefResponse[];
    messageConstants: any;
}

export interface DatonDefResponse
{
    name: string;
    isPersiston: boolean,
    mainTableDef: TableDefResponse;
    criteriaDef?: TableDefResponse;
    multipleMainRows: boolean;
}

export interface TableDefResponse
{
    name: string;
    permissionLevel: number,
    cols: ColDefResponse[];
    children?: TableDefResponse[]; 
    primaryKeyColName?: string;
    prompt?: string;
    isCriteria: boolean;
}

export interface ColDefResponse
{
    name: string;
    permissionLevel: number,
    wireType: string;
    isComputed: boolean; 
    allowSort: boolean;
    foreignKeyDatonTypeName: string;
    selectBehavior: SelectBehaviorResponse;
    leftJoin: LeftJoinResponse;
    isMainColumn: boolean;
    isVisibleInDropdown: boolean;
    prompt?: string;
    minLength?: number; 
    maxLength?: number;
    lengthValidationMessage?: string; 
    regex?: string;
    regexValidationMessage?: string;
    minNumberValue?: number;
    maxNumberValue?: number;
    rangeValidationMessage?: string;
    imageUrlColumName?: string;
}

export interface LeftJoinResponse {
    foreignKeyColumnName: string;
    remoteDisplayColumnName: string;
}

export interface SelectBehaviorResponse {
    viewonTypeName: string;
    autoCriterionName?: string;
    autoCriterionValueColumnName?: string;
    viewonValueColumnName: string;
    useDropdown: boolean;
}

export interface GetDatonResponse {
    condensedDaton: CondensedDatonResponse;
    key?: string;
    errors?: string[];
}

export interface CondensedDatonResponse
{
    isComplete?: boolean;
    //other members: see condensed daton spec
}

export interface ManageDatonResponse
{
    key: string;
    subscribeState: 0|1|2;
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
