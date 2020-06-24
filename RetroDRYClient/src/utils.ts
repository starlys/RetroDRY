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

//return a datetime component number into a 2-char string
function pad2(n: number){
    if (n < 10) return '0' + n;
    return n.toString();
}

//convert a wire datetime (YYYYMMDDMMHH) that must be a 12-char string into an 
//array of numbers [year, month, day, hour, minute]. The time is offset by the
//session timezone. The month is in range 1-12. Could throw exceptions.
function wireDateTimeToNumbers(session: Session, wire: string): number[] {
    //implementation notes: The point in time represented by the
    //Date instance is "wrong" because we're avoiding any browser zone conversons.
    const yr = parseInt(wire.substr(0, 4));
    const mo = parseInt(wire.substr(4, 2));
    const dy = parseInt(wire.substr(6, 2));
    const hr = parseInt(wire.substr(8, 2));
    const mi = parseInt(wire.substr(10, 2));
    const d = new Date(Date.UTC(yr, mo - 1, dy, hr, mi + session.timeZoneOffset, 0, 0));
    return [
        d.getUTCFullYear(),
        d.getUTCMonth() + 1,
        d.getUTCDate(),
        d.getUTCHours(),
        d.getUTCMinutes()
    ];
}

//convert a wire date (YYYYMMDD) to a readable date for display only; returns empty string if invalid or empty
export function wireDateToReadable(wire: string|null): string {
    return wireDateToInput(wire); //happens to be the same implementation
}

//convert a wire datetime (YYYYMMDDHHMM) to a readable datetime for display only, with timezone conversion;
//returns empty string if invalid or empty
export function wireDateTimeToReadable(session: Session, wire: string|null): string {
    if (!wire || wire.length !== 12) return '';
    try {
        const parts = wireDateTimeToNumbers(session, wire);
        return parts[0] + '-' + pad2(parts[1]) + '-' + pad2(parts[2]) + ' ' + pad2(parts[3]) + ':' + pad2(parts[4]);
    }  catch {
        return '';
    }
}

//convert a wire date (YYYYMMDD) to a date suitable for an input control; returns empty string if invalid or empty
export function wireDateToInput(wire: string|null): string {
    if (!wire || wire.length !== 8) return '';
    return wire.substr(0, 4) + '-' + wire.substr(4, 2) + '-' + wire.substr(6, 2);    
}

//convert a wire datetime (YYYYMMDDHHMM) to a date suitable for an input control and a time suitable for an input control; returns empty string if invalid or empty
export function wireDateTimeToDateTimeInputs(session: Session, wire: string|null): [string, string] {
    if (!wire || wire.length !== 12) return ['', ''];
    try {
        const parts = wireDateTimeToNumbers(session, wire);
        return [
            parts[0] + '-' + pad2(parts[1]) + '-' + pad2(parts[2]),
            pad2(parts[3]) + ':' + pad2(parts[4])
        ];
    }  catch {
        return ['', ''];
    }
}

//convert a value from a date input control (YYYY-M-D, allowing padding) to a wire date (YYYYMMDD), or null if invalid or empty
export function inputDateToWire(ctrlvalue: string|null): string|null {
    if (!ctrlvalue) return null;
    const parts = ctrlvalue.split('-');
    if (parts.length !== 3) return null;
    try {
        const yr = parseInt(parts[0]);
        const mo = parseInt(parts[1]);
        const dy = parseInt(parts[2]);
        return yr + pad2(mo) + pad2(dy);
    } catch {
        return null;
    }
}

//convert a value from a date input control (YYYY-M-D, allowing padding) and a time input control(h:m allowing padding and seconds)
//to a wire datetime (YYYYMMDDHHMM), or null if invalid or empty
export function inputDateTimeToWire(session: Session, dctrlvalue: string|null, tctrlvalue: string|null): string|null {
    if (!dctrlvalue) return null;
    if (!tctrlvalue) tctrlvalue = '00:00';
    const dparts = dctrlvalue.split('-');
    const tparts = tctrlvalue.split(':');
    if (dparts.length !== 3 || tparts.length < 2) return null;
    try {
        const yr = parseInt(dparts[0]);
        const mo = parseInt(dparts[1]);
        const dy = parseInt(dparts[2]);
        const hr = parseInt(tparts[0]);
        const mi = parseInt(tparts[1]);

        const d = new Date(Date.UTC(yr, mo - 1, dy, hr, mi - session.timeZoneOffset, 0, 0));
        return d.getUTCFullYear()
            + pad2(d.getUTCMonth() + 1)
            + pad2(d.getUTCDate())
            + pad2(d.getUTCHours())
            + pad2(d.getUTCMinutes());
    } catch {
        return null;
    }
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
//session is optional.
//Return is 2-element array with invalid message and bool flag indicating if any additional columns were updated
export async function processStringEntry(session: Session, tableDef: TableDefResponse, colDef: ColDefResponse, 
    row: any, invalidMemberName: string): Promise<[string|null, boolean]> {
    const value = row[colDef.name];
    const msg = validateString(colDef, value);
    const anyCascades = await afterSetRowValue(session, tableDef, colDef, row, invalidMemberName, msg, null);
    return [msg, anyCascades];
}

//if number ok, sets row value; if bad, sets row invalid message and returns message.
//session is optional.
//Return is 2-element array with invalid message and bool flag indicating if any additional columns were updated
export async function processNumberEntry(session: Session, tableDef: TableDefResponse, colDef: ColDefResponse, 
    baseType: string, row: any, invalidMemberName: string): Promise<[string|null, boolean]> {
    const [msg, value] = validateNumber(colDef, baseType, row[colDef.name]);
    row[colDef.name] = value; //this might round a non-integer entry in an int field, for example
    const anyCascades = await afterSetRowValue(session, tableDef, colDef, row, invalidMemberName, msg, null);
    return [msg, anyCascades];
}

//if number range for criterion is ok, sets row value; if bad, sets row invalid message and returns message.
//session is optional.
//Return is 2-element array with invalid message and bool flag indicating if any additional columns were updated
export async function processNumberRangeEntry(session: Session, tableDef: TableDefResponse, colDef: ColDefResponse, 
    baseType: string, row: any, invalidMemberName: string): Promise<[string|null, boolean]> {
    const [msg, value] = validateNumberRange(colDef, baseType, row[colDef.name]);
    row[colDef.name] = value; //this might round a non-integer entry in an int field, for example
    const anyCascades = await afterSetRowValue(session, tableDef, colDef, row, invalidMemberName, msg, null);
    return [msg, anyCascades];
}

//if date/datetime or date/datetime range (as wire string) is ok, returns null; if bad, sets row invalid message and returns message.
//session is optional.
//Return is 2-element array with invalid message and bool flag indicating if any additional columns were updated
export async function processDateTimeEntry(session: Session, tableDef: TableDefResponse, colDef: ColDefResponse, 
    row: any, invalidMemberName: string): Promise<[string|null, boolean]> {
    const value = row[colDef.name];
    let msg = null;
    if (!value && (colDef.wireType === 'datetime' || colDef.wireType == 'date')) 
        msg = colDef.prompt || '!';
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
        msg = null; //other types don't have local validation: bool, date/times
    
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
//if there is no tilde, return the same value for lo and hi; will return nulls instead of empty strings
export function splitOnTilde(s: string) {
    let h = s.indexOf('~');
    if (h < 0) return[s, s];
    let lo: string|null = s.substr(0, h);
    let hi: string|null = s.substr(h + 1);
    if (lo.length === 0) lo = null;
    if (hi.length === 0) hi = null;
    return [lo, hi];
}

