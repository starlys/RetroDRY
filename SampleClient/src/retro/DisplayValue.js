import React from 'react';

//Formats a value from a row to readonly output
//props.colDef is the ColDefResponse
//props.row is the daton row
//props.width is the css width string
export default (props) => {
    const {colDef, row, width} = props;
    const value = row[colDef.name];
    const wrapStyle = {width: width};
    if (colDef.wireType === 'bool') return <input className="card-value" type="checkbox" readOnly checked={value}/>
    return <span className="card-value" style={wrapStyle}>{value}</span>;
};
//todo all types
//todo float format, date format