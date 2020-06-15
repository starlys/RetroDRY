import React from 'react';
import {getBaseType} from 'retrodry';

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
export default (props) => {
    const {colDef, row, width} = props;
    const value = row[colDef.name];
    const wrapStyle = {width: width};
    const baseType = getBaseType(colDef.wireType);
    if (baseType === 'bool' ) return <input className="card-value" type="checkbox" readOnly checked={value}/>;
    if (baseType === 'string') {
        let html = textToHtml(value);
        return <span className="card-value" style={wrapStyle} dangerouslySetInnerHTML={{__html: html}}></span>;
    }
    return <span className="card-value" style={wrapStyle}>{value}</span>;
};
//todo all types
//todo float format, date format