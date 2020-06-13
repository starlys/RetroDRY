import React, {useState} from 'react';

//return a 2-element array with low and high string values for a criterion that supports tilde-delimited parts
function splitOnTilde(s) {
    let h = s.indexOf('~');
    if (h < 0) return[null, null];
    let lo = s.substr(0, h), hi = s.substr(h + 1);
    if (lo.length === 0) lo = null;
    if (hi.length === 0) hi = null;
    return [lo, hi];
}

//if string is ok, sets criset value and returns null; if bad, sets criset invalid message and returns message
//todo current implementation never allows for invalid
function processString(colDef, criset, stringValue, invalidMemberName) {
    const s = stringValue ?? '';
    criset[colDef.name] = s;
    delete criset[invalidMemberName];
    return null;
}

//if number range ok, sets criset value and returns null; if bad, sets criset invalid message and returns message
//todo current implementation never allows for invalid
function processNumberRange(colDef, baseType, criset, lo, hi, invalidMemberName) {
    const isFloat = baseType === 'double' || baseType === 'decimal'; 
    if (lo) {
        const loN = !isFloat ? parseInt(lo, 10) : parseFloat(lo);
        if (isNaN(loN)) lo = null;
        else lo = loN.toString();
    } else 
        lo = null;
    if (hi) {
        const hiN = !isFloat ? parseInt(hi, 10) : parseFloat(hi);
        if (isNaN(hiN)) hi = null;
        else hi = hiN.toString();
    } else 
        hi = null;    
    criset[colDef.name] = (lo || hi) ? lo + '~' + hi : null;
    delete criset[invalidMemberName];
    return null;
}

//Show an editor for a single viewon criterion of any type and maintains its value in criset[colDef.name]; it also maintains
//the invalid message, for example for column 'firstName' the invalid message is in criset['firstName$v'].
//props.colDef is the ColDefResponse (metadata for the column in the criteria quasi-table)
//props.criset is the criset object being edited 
export default React.memo((props) => {
    const {colDef, criset} = props;
    const invalidMemberName = colDef.name + '$v';
    const [invalidMessage, setInvalidMessage] = useState(criset[invalidMemberName]);
    const containerClass = invalidMessage ? 'invalid inputwrap' : 'valid inputwrap';

    //convert nint32 to int32, etc.
    let baseType = colDef.wireType;
    if (baseType[0] === 'n') baseType = baseType.substr(1); 

    if (baseType === 'bool') {
        const boolChanged = (ev) => criset[colDef.name] = ev.target.value;
        return <span className={containerClass} >
            <select defaultValue={criset[colDef.name]} onChange={boolChanged}>
                <option value="">Any</option>
                <option value="1">Yes</option>
                <option value="0">No</option>
            </select>
        </span>;
    }

    else if (baseType === 'byte' || baseType === 'int16' || baseType === 'int32' || baseType === 'int64'
        || baseType === 'double' || baseType === 'decimal') {
        const loHi = splitOnTilde(criset[colDef.name] || '');
        const loNumberChanged = (ev) => {
            setInvalidMessage(processNumberRange(colDef, baseType, criset, ev.target.value, loHi[1], invalidMemberName));
        };
        const hiNumberChanged = (ev) => {
            setInvalidMessage(processNumberRange(colDef, baseType, criset, loHi[0], ev.target.value, invalidMemberName));
        };
        return <span className={containerClass} >
            <input className="criterion number" type="number" defaultValue={loHi[0]} onChange={loNumberChanged} /> - &nbsp;
            <input className="criterion number" type="number" defaultValue={loHi[1]} onChange={hiNumberChanged} />
        </span>;
    }

    else if (baseType === 'string') {
        const stringChanged = (ev) => {
            setInvalidMessage(processString(colDef, criset, ev.target.value, invalidMemberName));
        };
        return <span className={containerClass}>
            <input className="criterion" defaultValue={criset[colDef.name]} onChange={stringChanged} />
        </span>;
    }

    else if (baseType === 'date') { //todo
        const stringChanged = (ev) => {
            setInvalidMessage(processString(colDef, criset, ev.target.value, invalidMemberName));
        };
        return <span className={containerClass} >
            <input defaultValue={criset[colDef.name]} onChange={stringChanged} />
        </span>;
    }

    else if (baseType === 'datetime') { //todo
        const stringChanged = (ev) => {
            setInvalidMessage(processString(colDef, criset, ev.target.value, invalidMemberName));
        };
        return <span className={containerClass} >
            <input defaultValue={criset[colDef.name]} onChange={stringChanged} />
        </span>;
    }
     
    else return null;
});
