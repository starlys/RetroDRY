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
export default React.memo((props) => {
    const {colDef, row, width, layer} = props;
    const invalidMemberName = getInvalidMemberName(colDef);
    const [hasFocus, setHasFocus] = useState(false);
    const [, setInvalidMessage] = useState(row[invalidMemberName]);
    const containerClass = row[invalidMemberName] ? 'invalid inputwrap' : 'valid inputwrap';
    const wrapStyle = {width: width};
    const baseType = getBaseType(colDef.wireType);

    //event handlers
    const ctrlFocused = () => setHasFocus(true);
    const ctrlBlurred = () => setHasFocus(false);
    const boolChanged = (ev) => row[colDef.name] = ev.target.checked;
    const numberChanged = (ev) => {
        processNumberEntry(layer?.stackstate?.session, props.tableDef, colDef, baseType, row, ev.target.value, invalidMemberName).then(pe => {
            const [msg, anyCascades] = pe;
            setInvalidMessage(msg);
            if (anyCascades && layer) layer.stackstate.callOnChanged();
        });
    };
    const stringChanged = (ev) => {
        setInvalidMessage(processStringEntry(colDef, row, ev.target.value, invalidMemberName));
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
        inputCtrl = <input type="number" defaultValue={row[colDef.name]} onChange={numberChanged} onFocus={ctrlFocused} onBlur={ctrlBlurred}/>;
    }
    else if (baseType === 'string') {
        //string renders multiline control if max length is > 200
        if (colDef.maxLength > 200) {
            inputCtrl = <textarea defaultValue={row[colDef.name]} onChange={stringChanged} onFocus={ctrlFocused} onBlur={ctrlBlurred} />;
        } else {
            inputCtrl = <input defaultValue={row[colDef.name]} onChange={stringChanged} onFocus={ctrlFocused} onBlur={ctrlBlurred} />;
        }
    }
    else if (baseType === 'date') { //todo
        inputCtrl = <input defaultValue={row[colDef.name]} onChange={stringChanged} onFocus={ctrlFocused} onBlur={ctrlBlurred}/>;
    }
    else if (baseType === 'datetime') { //todo
        inputCtrl = <input defaultValue={row[colDef.name]} onChange={stringChanged} onFocus={ctrlFocused} onBlur={ctrlBlurred}/>;
    }

    return (
        <span className={containerClass} style={wrapStyle}>
            {popupError}
            {inputCtrl}
            {lookupButton}
        </span>);
});
