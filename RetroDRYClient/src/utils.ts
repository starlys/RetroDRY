import { ColDefResponse, DatonDefResponse, TableDefResponse } from "./wireTypes";
import { TableRecurPointFromDaton, RowRecurPoint, TableRecurPoint } from "./recurPoint";
import Session from "./session";

//file contains top level public functions for use by client app

//true if the given base type is numeric (does not test for nint16 and other nullable types)
export function isNumericBaseType(baseType: string) {
    return baseType === 'byte' || baseType === 'int16' || baseType === 'int32' || baseType === 'int64'
        || baseType === 'double' || baseType === 'decimal';
}

//get the name to use for members of row objects to store validation messages
export function getInvalidMemberName(colDef: ColDefResponse): string {
    return colDef.name + '$v';
}

//get the base type name for a nullable or non-nullable type. For example given nstring, returns string
export function getBaseType(wireTypeName: string): string {
    let baseType = wireTypeName;
    if (baseType[0] === 'n') baseType = baseType.substr(1); 
    return baseType;
}

//validate a string against the validation rules in colDef, and return null if ok or an error message
export function validateString(colDef: ColDefResponse, value: string): string|null {
    const s = value || '';
    const len = s.length;
    let msg:string|null = null;
    const minLengthOk = !colDef.minLength || len >= colDef.minLength;
    const maxLengthOk = !colDef.maxLength || len <= colDef.maxLength;
    if (!minLengthOk || !maxLengthOk) {
        msg = colDef.lengthValidationMessage || '{0}: {2}-{1}';
        msg = msg.replace('{0}', colDef.prompt || '?').replace('{1}', (colDef.maxLength || 0).toString())
            .replace('{2}', (colDef.minLength || 0).toString());
    } else {
        //check regex only if length was ok
        if (colDef.regex) {
            const regex = new RegExp(colDef.regex);
            if (!regex.test(s)) {
                msg = colDef.regexValidationMessage || '!!!';
                msg = msg.replace('{0}', colDef.prompt || '?');
            }
        }
    }
    return msg;
}

//validate a number against the validation rules in colDef, and return null if ok or an error message;
//value can be the user entry (string) or a number if pulled from the row object;
//returns 2-element array with error message and corrected value for storage
export function validateNumber(colDef: ColDefResponse, baseType: string, value: any): [string|null, any] {
    let minOfType = 0, maxOfType = 0, isInt = true;
    const stringValue = (value || '').toString();
    if (baseType === 'byte') { maxOfType = 255; }
    else if (baseType === 'int16') { minOfType = -32768; maxOfType = 32767; }
    else if (baseType === 'int32') { minOfType = -2147483648; maxOfType = 2147483647; }
    else if (baseType === 'int64') { minOfType = Number.MIN_SAFE_INTEGER; maxOfType = Number.MAX_SAFE_INTEGER; }
    else if (baseType === 'double') { isInt = false; }
    else if (baseType === 'decimal') { isInt = false; }
    else return [null, null];

    //special case - is ok if blank and allows nulls
    const isNullable = colDef.wireType !== baseType;
    const isNull = stringValue.trim().length === 0;
    if (isNull && isNullable) 
        return [null, null];

    //check numeric range
    const n = isInt ? parseInt(stringValue, 10) : parseFloat(stringValue);
    const useRange = colDef.minNumberValue && colDef.maxNumberValue;
    let min = 0, max = 0, ok: boolean;
    if (isInt) {
        min = useRange ? Math.max(minOfType, colDef.minNumberValue) : minOfType;
        max = useRange ? Math.min(maxOfType, colDef.maxNumberValue) : maxOfType;
        ok = !isNaN(n) && n >= min && n <= max;
    } else {
        ok = !isNaN(n);
        if (ok && useRange) ok = n >= colDef.minNumberValue && n <= colDef.maxNumberValue;
        min = useRange ? colDef.minNumberValue : -99999; //these 99999s affect validation message only
        max = useRange ? colDef.maxNumberValue : 99999;
    }

    //format error message
    if (ok) return [null, n];
    const standardMsg = isNull ? '{0}' : '{0}: {1}-{2}'
    let msg = colDef.rangeValidationMessage || standardMsg;
    msg = msg.replace('{0}', colDef.prompt || '?').replace('{1}', min.toString()).replace('{2}', max.toString());
    return [msg, n];
}

//validate a number against the validation rules in colDef, and return null if ok or an error message;
//value is the combined user entry (string in form lo~hi);
//returns 2-element array with error message and corrected value for storage
//(note current implementation never returns errors)
export function validateNumberRange(colDef: ColDefResponse, baseType: string, value: string|null) {
    const isFloat = baseType === 'double' || baseType === 'decimal'; 
    let [lo, hi] = splitOnTilde(value || '');
    if (lo) {
        const loN = !isFloat ? parseInt(lo, 10) : parseFloat(lo);
        if (isNaN(loN)) lo = null;
        else lo = loN.toString();
    } else 
        lo = '';
    if (hi) {
        const hiN = !isFloat ? parseInt(hi, 10) : parseFloat(hi);
        if (isNaN(hiN)) hi = null;
        else hi = hiN.toString();
    } else 
        hi = '';
    value = null;
    if (lo || hi) {
        if (lo === hi) value = lo;
        else value = lo + '~' + hi;
    }
    return [null, value];
}

//set any leftjoin-defined columns in row from sourceRow (which is from the lookup viewon, or may be missing).
//Note that for performance, the viewon is assumed to contain the needed description columns when one was used; but if
//the user entered the key value manually, then it loads the persiston and assumes the single main row of that persiston
//also contains the needed description columns.
//session is optional
//returns 0 if lookup failed, 1 if ok but nothing changed, or 2 if this caused cascaded changes to editingRow
async function setLookupDescription(session: Session, editingTableDef: TableDefResponse, editingColDef: ColDefResponse, 
    editingRow: any, sourceRow: any): Promise<number> {
    //collect description columns
    const descrColDefs = editingTableDef.cols.filter(c => c.leftJoinForeignKeyColumnName === editingColDef.name);
    if (descrColDefs.length === 0) return 1;
    
    //if viewon row not given, get main row of persiston
    let sourceIsValid = !!sourceRow;
    if (!sourceIsValid && session) {
        const sourcePersiston = await session.get(editingColDef.foreignKeyDatonTypeName + '|=' + editingRow[editingColDef.name]);
        sourceIsValid = !!sourcePersiston;
        sourceRow = sourcePersiston || {}; //might be missing
    }

    //copy from source row to editing row; if sourceRow was undefined, this will have the effect of clearing the description columns
    let anyChanges = false;
    for (let descrColDef of descrColDefs) {
        const descrValue = sourceRow[descrColDef.leftJoinRemoteDisplayColumnName];
        editingRow[descrColDef.name] = descrValue || null;
        anyChanges = true;
    }
    if (!sourceIsValid) return 0;
    return anyChanges ? 2 : 1;
}

//Set or clear the invalid message for the value, then optionally cascade to other changes based on this change. 
//All code paths that change row values should go through here.
//In the case of user editing, the input element causes row updates on each keystroke, but only calls this after editing is done.
//In the case of programmatic changes, the caller must also cause the daton view to rerender.
//invalidMemberName must be from getInvalidMemberName().
//invalidMessage is falsy if the value is ok.
//lookupRow is only used if the value is a foreign key and it is being set from a lookup viewon
//returns true if this edit cascaded to any additional column changes
export async function afterSetRowValue(session:Session, tableDef: TableDefResponse, colDef: ColDefResponse, row: any,  
    invalidMemberName: string, invalidMessage: string|null,
    lookupRow: any): Promise<boolean> {
    let ok = !invalidMessage;
    let anyCascades = false;

    //cascade changes to description cols for a changed lookup value;
    //this might change ok and invalid message
    if (ok && !tableDef.isCriteria && colDef.foreignKeyDatonTypeName) {
        const lookupCascadeResult = await setLookupDescription(session, tableDef, colDef, row, lookupRow);
        anyCascades = lookupCascadeResult === 2;
        if (lookupCascadeResult === 0) {
            ok = false;
            invalidMessage = 'Lookup value does not exist'; //todo language
        }
    }

    if (ok) {
        delete row[invalidMemberName];
    } else {
        row[invalidMemberName] = invalidMessage;
    }
    return anyCascades;
}

//if string is ok, returns null; if bad, sets row invalid message and returns message.
//session is optinal.
//Return is 2-element array with invalid message and bool flag indicating if any additional columns were updated
export async function processStringEntry(session: Session, tableDef: TableDefResponse, colDef: ColDefResponse, 
    row: any, invalidMemberName: string): Promise<[string|null, boolean]> {
    const value = row[colDef.name];
    const msg = validateString(colDef, value);
    const anyCascades = await afterSetRowValue(session, tableDef, colDef, row, invalidMemberName, msg, null);
    return [msg, anyCascades];
}

//if number ok, sets row value; if bad, sets row invalid message and returns message.
//session is optinal.
//Return is 2-element array with invalid message and bool flag indicating if any additional columns were updated
export async function processNumberEntry(session: Session, tableDef: TableDefResponse, colDef: ColDefResponse, 
    baseType: string, row: any, invalidMemberName: string): Promise<[string|null, boolean]> {
    const [msg, value] = validateNumber(colDef, baseType, row[colDef.name]);
    row[colDef.name] = value; //this might round a non-integer entry in an int field, for example
    const anyCascades = await afterSetRowValue(session, tableDef, colDef, row, invalidMemberName, msg, null);
    return [msg, anyCascades];
}

//if number range for criterion is ok, sets row value; if bad, sets row invalid message and returns message.
//session is optinal.
//Return is 2-element array with invalid message and bool flag indicating if any additional columns were updated
export async function processNumberRangeEntry(session: Session, tableDef: TableDefResponse, colDef: ColDefResponse, 
    baseType: string, row: any, invalidMemberName: string): Promise<[string|null, boolean]> {
    const [msg, value] = validateNumberRange(colDef, baseType, row[colDef.name]);
    row[colDef.name] = value; //this might round a non-integer entry in an int field, for example
    const anyCascades = await afterSetRowValue(session, tableDef, colDef, row, invalidMemberName, msg, null);
    return [msg, anyCascades];
}

//wrapper for validateXX functions; sets invalid message in row and returns message, but if there was already a message there, just use it
function validateAnyType(colDef: ColDefResponse, isCriteria: boolean, row: any, baseType: string, value: any, invalidMemberName: string) {
    let msg: string|null|undefined = row[invalidMemberName];
    if (msg) return msg;

    if (baseType === 'string') 
        msg = validateString(colDef, value);
    else if (isNumericBaseType(baseType)) {
        if (isCriteria)
            [msg, ] = validateNumberRange(colDef, baseType, value);
        else
            [msg, ] = validateNumber(colDef, baseType, value);
    } else
        msg = null; //todo other types
    
    if (msg) row[invalidMemberName] = msg;
    return msg;
}

//get all local validation errors on a daton; this revalidates every row/col including unchanged values
//and returns the error list; also sets validation messages in the rows (see getInvalidMemberName)
export function validateAll(datonDef: DatonDefResponse, daton: any): string[] {
    const errors:string[] = []; 
    if (datonDef.multipleMainRows) {
        const rt = TableRecurPointFromDaton(datonDef, daton);
        getLocalValidationErrors_table(rt, errors);
    } else { 
        const rr = new RowRecurPoint(datonDef.mainTableDef, daton);
        getLocalValidationErrors_row(rr, errors);
    }
    return errors;
}

//get all local validation errors on a criteria set; also sets validation messages in the rows (see getInvalidMemberName)
export function validateCriteria(criteriaDef: TableDefResponse, criset: any): string[] {
    const errors:string[] = []; 
    for (let colDef of criteriaDef.cols) {
        const value = criset[colDef.name];
        const baseType = getBaseType(colDef.wireType);
        const msg = validateAnyType(colDef, true, criset, baseType, value, getInvalidMemberName(colDef));
        if (msg) errors.push(msg);
    }
    return errors;
}

//see validateAll
function getLocalValidationErrors_row(rr: RowRecurPoint, errors: string[]) {
    for (let colDef of rr.tableDef.cols) {
        const value = rr.row[colDef.name];
        const baseType = getBaseType(colDef.wireType);
        const msg = validateAnyType(colDef, false, rr.row, baseType, value, getInvalidMemberName(colDef));
        if (msg) errors.push(msg);
    }
    for (let rt of rr.getChildren()) {
        getLocalValidationErrors_table(rt, errors);
    }
}

//see validateAll
function getLocalValidationErrors_table(rt: TableRecurPoint, errors: string[]) {
    for (let rr of rt.getRows())
        getLocalValidationErrors_row(rr, errors);
}

//return a 2-element array with low and high string values for a criterion that supports tilde-delimited parts;
//if there is no tilde, return the same value for lo and hi
export function splitOnTilde(s: string) {
    let h = s.indexOf('~');
    if (h < 0) return[s, s];
    let lo: string|null = s.substr(0, h);
    let hi: string|null = s.substr(h + 1);
    if (lo.length === 0) lo = null;
    if (hi.length === 0) hi = null;
    return [lo, hi];
}
