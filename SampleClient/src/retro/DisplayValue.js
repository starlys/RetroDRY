import React, {useState} from 'react';
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
//props.session is the retrodry Session 
export default (props) => {
    const {colDef, row, width, session} = props;
    const [selectSource, setSelectSource] = useState(null); //only for select-type inputs; the array of rows to select from
    const [selectValueCol, setSelectValueCol] = useState(null); //only for select-type inputs; the column name of the value (primary key)
    const [selectDisplayCol, setSelectDisplayCol] = useState([]); //only for select-type inputs; the column name of the display value

    const value = row[colDef.name];
    const wrapStyle = {width: width};
    const baseType = getBaseType(colDef.wireType);

    //determine from metadata if this is a dropdown select
    if (!selectSource && !colDef.lookupViewonTypeName && colDef.foreignKeyDatonTypeName) {
        const selectDef = session.getDatonDef(colDef.foreignKeyDatonTypeName);
        if (selectDef && selectDef.multipleMainRows) {
            session.get(colDef.foreignKeyDatonTypeName + '|+', {doSubscribeEdit: true}).then(d => {
                setSelectSource(d[selectDef.mainTableDef.name]); //this is the array of rows, not the top level of the whole-table persiston
                setSelectValueCol(selectDef.mainTableDef.primaryKeyColName);
                const displayCol = selectDef.mainTableDef.cols.find(c => c.isMainColumn);
                setSelectDisplayCol(displayCol.name);
            }); 
        }
    }

    if (selectSource) 
        return <span className="card-value" style={wrapStyle}>{selectSource.filter(r => r[selectValueCol] === value).map(r => r[selectDisplayCol])}</span>;
    else if (baseType === 'bool')
        return <input className="card-value" type="checkbox" readOnly checked={value}/>;
    if (baseType === 'string') {
        let html = textToHtml(value);
        return <span className="card-value" style={wrapStyle} dangerouslySetInnerHTML={{__html: html}}></span>;
    }
    return <span className="card-value" style={wrapStyle}>{value}</span>;
};
//todo all types
//todo float format, date format