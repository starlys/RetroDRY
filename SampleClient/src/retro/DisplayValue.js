import React, {useState} from 'react';
import {DropdownState, getBaseType, wireDateToReadable, wireDateTimeToReadable} from 'retrodryclient';

function textToHtml(s) {
    return (s || '').replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/'/g, '&#39;')
        .replace(/"/g, '&#34;')
        .replace(/\r/g, '')
        .replace(/\n/g, '<br/>');
};

//Formats a value from a row to readonly output
//props.colDef is the ColDefResponse
//props.row is the daton row
//props.width is the css width string
//props.session is the retrodry Session 
const Component = (props) => {
    const {colDef, row, width, session} = props;
    const [displayAs, setDisplayAs] = useState(null); //mapped value via DropdownState, if any

    let value = row[colDef.name];
    const wrapStyle = {width: width};
    let baseType = getBaseType(colDef.wireType);
    if (displayAs) { value = displayAs; baseType = 'string'; }

    //determine from metadata if this requires a lookup to another persiston or viewon
    const mightUseDDState = colDef.selectBehavior || colDef.foreignKeyDatonTypeName;
    if (!displayAs && mightUseDDState) {
        const ddstate = new DropdownState(session, row, colDef);
        ddstate.getDisplayValue(value).then((d) => {
            if (d !== null) setDisplayAs(d); 
        });
    }

    if (baseType === 'bool')
        return <input className="card-value" type="checkbox" readOnly disabled={true} checked={value}/>;
    else if (baseType === 'string') {
        let html = textToHtml(value);
        return <span className="card-value" style={wrapStyle} dangerouslySetInnerHTML={{__html: html}}></span>;
    } else if (baseType === 'date') {
        const readable = wireDateToReadable(value)
        return <span className="card-value" style={wrapStyle}>{readable}</span>;
    } else if (baseType === 'datetime') {
        const readable = wireDateTimeToReadable(session, value)
        return <span className="card-value" style={wrapStyle}>{readable}</span>;
    } else if (baseType === 'double' || baseType === 'decimal') {
        //future feature: format floats with the right number of decimal places; for now, always assume 2
        const readable = value ? value.toFixed(2) : '';
        return <span className="card-value" style={wrapStyle}>{readable}</span>;
    }

    //default for numbers
    return <span className="card-value" style={wrapStyle}>{value}</span>;
};

export default Component;