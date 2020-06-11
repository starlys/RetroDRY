import React, {useState} from 'react';

//if string is ok, sets row value and returns null; if bad, sets row invalid message and returns message
function processString(colDef, row, stringValue, invalidMemberName) {
    const s = stringValue ?? '';
    const len = s.length;
    let msg = null, ok = true;
    const minLengthOk = !colDef.minLength || len >= colDef.minLength;
    const maxLengthOk = !colDef.maxLength || len <= colDef.maxLength;
    if (!minLengthOk || !maxLengthOk) {
        ok = false;
        msg = colDef.lengthValidationMessage ?? '{0}: {2}-{1}';
        msg = msg.replace('{0}', colDef.prompt).replace('{1}', colDef.maxLength).replace('{2}', colDef.minLength);
    } else {
        //check regex only if length was ok
        if (colDef.regex) {
            const regex = new RegExp(colDef.regex);
            if (!regex.test(s)) {
                ok = false;
                msg = colDef.regexValidationMessage ?? '!!!';
                msg = msg.replace('{0}', colDef.prompt)
            }
        }
    }
    if (ok) {
        row[colDef.name] = s;
        delete row[invalidMemberName];
    } else {
        row[invalidMemberName] = msg;
    }
    return msg;
}

//if number ok, sets row value and returns null; if bad, sets row invalid message and returns message
function processNumber(colDef, baseType, row, stringValue, invalidMemberName) {
    let minOfType, maxOfType, isInt = true;
    if (baseType === 'byte') { minOfType = 0; maxOfType = 255; }
    else if (baseType === 'int16') { minOfType = -32768; maxOfType = 32767; }
    else if (baseType === 'int32') { minOfType = -2147483648; maxOfType = 2147483647; }
    else if (baseType === 'int64') { minOfType = Number.MIN_SAFE_INTEGER; maxOfType = Number.MAX_SAFE_INTEGER; }
    else if (baseType === 'double') { isInt = false; }
    else if (baseType === 'decimal') { isInt = false; }
    else return null;

    const n = isInt ? parseInt(stringValue, 10) : parseFloat(stringValue);
    const useRange = colDef.minNumberValue && colDef.maxNumberValue;
    let min, max, ok;
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
    let msg = null;
    if (ok) {
        row[colDef.name] = n;
        delete row[invalidMemberName];
    } else {
        msg = colDef.rangeValidationMessage || '{0}: {1}-{2}';
        msg = msg.replace('{0}', colDef.prompt).replace('{1}', min).replace('{2}', max);
        row[invalidMemberName] = msg;
    }
    return msg;
}

//Show an editor for a single value of any type and maintains its value in row[colDef.name]; it also maintains
//the invalid message, for example for column 'firstName' the invalid message is in row['firstName$v'].
//props.colDef is the ColDefResponse (metadata for the column)
//props.row is the row object being edited
//props.width is the css width string
export default React.memo((props) => {
    const {colDef, row, width} = props;
    const invalidMemberName = colDef.name + '$v';
    const [invalidMessage, setInvalidMessage] = useState(row[invalidMemberName]);
    const containerClass = invalidMessage ? 'invalid inputwrap' : 'valid inputwrap';
    const wrapStyle = {width: width};

    //convert nint32 to int32, etc.
    let baseType = colDef.wireType;
    if (baseType[0] === 'n') baseType = baseType.substr(1); 

    if (baseType === 'bool') {
        const boolChanged = (ev) => row[colDef.name] = ev.target.checked;
        return <span className={containerClass} style={wrapStyle}>
            <input type="checkbox" checked={row[colDef.name]} onChange={boolChanged} />
        </span>;
    }

    else if (baseType === 'byte' || baseType === 'int16' || baseType === 'int32' || baseType === 'int64'
        || baseType === 'double' || baseType === 'decimal') {
        const numberChanged = (ev) => {
            setInvalidMessage(processNumber(colDef, baseType, row, ev.target.value, invalidMemberName));
        };
        return <span className={containerClass} style={wrapStyle}>
            <input type="number" defaultValue={row[colDef.name]} onChange={numberChanged} />
        </span>;
    }

    else if (baseType === 'string') {
        const stringChanged = (ev) => {
            setInvalidMessage(processString(colDef, row, ev.target.value, invalidMemberName));
        };
        return <span className={containerClass} style={wrapStyle}>
            <input defaultValue={row[colDef.name]} onChange={stringChanged} />
        </span>;
    }

    else if (baseType === 'date') { //todo
        const stringChanged = (ev) => {
            setInvalidMessage(processString(colDef, row, ev.target.value, invalidMemberName));
        };
        return <span className={containerClass} style={wrapStyle}>
            <input defaultValue={row[colDef.name]} onChange={stringChanged} />
        </span>;
    }

    else if (baseType === 'datetime') { //todo
        const stringChanged = (ev) => {
            setInvalidMessage(processString(colDef, row, ev.target.value, invalidMemberName));
        };
        return <span className={containerClass} style={wrapStyle}>
            <input defaultValue={row[colDef.name]} onChange={stringChanged} />
        </span>;
    }
     
    else return null;
});

//todo all validation rules
//todo show invalid message as popup when focused
//todo permissions 