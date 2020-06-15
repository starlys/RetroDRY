import React, {useState} from 'react';
import EditorPopupError from './EditorPopupError';
import {getInvalidMemberName, processNumberEntry, processStringEntry, getBaseType, isNumericBaseType} from 'retrodry';

//Show an editor for a single value of any type and maintains its value in row[colDef.name]; it also maintains
//the invalid message, for example for column 'firstName' the invalid message is in row['firstName$v'].
//props.tableDef is the TableDefResponse which is the metadata for props.row
//props.colDef is the ColDefResponse (metadata for the column)
//props.row is the row object being edited
//props.width is the css width string
//props.layer is the optional DatonStackState layer data for the containing stack (can be omitted if this is used outside a stack)
//props.onChanged is called with no arguments after a change is saved to the row (on each keystroke)
export default (props) => {
    const {colDef, row, width, layer} = props;
    const invalidMemberName = getInvalidMemberName(colDef);
    const [hasFocus, setHasFocus] = useState(false);
    const [valueAtFocus, setValueAtFocus] = useState(null);
    const [, setInvalidMessage] = useState(row[invalidMemberName]);
    const containerClass = row[invalidMemberName] ? 'invalid inputwrap' : 'valid inputwrap';
    const wrapStyle = {width: width};
    const baseType = getBaseType(colDef.wireType);

    //event handlers
    const ctrlFocused = () => {
        setHasFocus(true);
        setValueAtFocus(row[colDef.name]);
    };
    const boolChanged = (ev) => row[colDef.name] = ev.target.checked;
    const numberChanged = (ev) => {
        row[colDef.name] = parseFloat(ev.target.value);
        props.onChanged();
    };
    const stringChanged = (ev) => {
        row[colDef.name] = ev.target.value;
        props.onChanged();
    };
    const afterEntryProcessed = ([msg, anyCascades]) => {
        setInvalidMessage(msg);
        if (anyCascades && layer) layer.stackstate.callOnChanged(); 
    };
    const ctrlBlurred = () => {
        setHasFocus(false);

        //validate on tab-out if any change
        if (valueAtFocus !== row[colDef.name]) {
            if (isNumericBaseType(baseType)) {
                processNumberEntry(layer?.stackstate?.session, props.tableDef, colDef, baseType, row, invalidMemberName).then(afterEntryProcessed);
            } else if (baseType === 'string') {
                processStringEntry(layer?.stackstate?.session, props.tableDef, colDef, row, invalidMemberName).then(afterEntryProcessed);
            }
        }
    };
    const startLookup = () => {
        layer.stackstate.startLookup(layer, props.tableDef, row, colDef);
    };

    //setup components that appear before/after the main control
    const popupError = <EditorPopupError show={hasFocus} message={row[invalidMemberName]}/>;
    let lookupButton = null;
    if (colDef.lookupViewonTypeName && layer) {
        //todo width problem here
        lookupButton = <button className="btn-lookup" onClick={startLookup}>..</button>;
    }

    //setup main input control
    let inputCtrl = null;
    if (baseType === 'bool') {
        inputCtrl = <input type="checkbox" checked={row[colDef.name]} onChange={boolChanged} onFocus={ctrlFocused} onBlur={ctrlBlurred}/>;
    }
    else if (isNumericBaseType(baseType)) {
        inputCtrl = <input type="number" value={row[colDef.name] || ''} onChange={numberChanged} onFocus={ctrlFocused} onBlur={ctrlBlurred}/>;
    }
    else if (baseType === 'string') {
        //string renders multiline control if max length is > 200
        if (colDef.maxLength > 200) {
            inputCtrl = <textarea value={row[colDef.name] || ''} onChange={stringChanged} onFocus={ctrlFocused} onBlur={ctrlBlurred} />;
        } else {
            inputCtrl = <input value={row[colDef.name] || ''} onChange={stringChanged} onFocus={ctrlFocused} onBlur={ctrlBlurred} />;
        }
    }
    else if (baseType === 'date') { //todo
        inputCtrl = <input value={row[colDef.name] || ''} onChange={stringChanged} onFocus={ctrlFocused} onBlur={ctrlBlurred}/>;
    }
    else if (baseType === 'datetime') { //todo
        inputCtrl = <input value={row[colDef.name] || ''} onChange={stringChanged} onFocus={ctrlFocused} onBlur={ctrlBlurred}/>;
    }

    return (
        <span className={containerClass} style={wrapStyle}>
            {popupError}
            {inputCtrl}
            {lookupButton}
        </span>);
};
