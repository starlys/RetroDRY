import React, {useState} from 'react';
import EditorPopupError from './EditorPopupError';
import {splitOnTilde, getInvalidMemberName, processNumberEntry, processNumberRangeEntry, processStringEntry, getBaseType, isNumericBaseType} from 'retrodry';

//Get the columns to be shown in a dropdown 
function getDropdownColumns(tableDef) {
    const ret = [];
    const mainCol = tableDef.cols.find(c => c.isMainColumn);
    if (mainCol) ret.push(mainCol.name);
    for (let secondaryCol of tableDef.cols.filter(c => c.isVisibleInDropdown))
        ret.push(secondaryCol.name);
    return ret;
}

//modify row so the hi range is the same as the lo, if hi wasn't set
function autoPopulateHiFromLo(row, colDef) {
    const value = row[colDef.name];
    if (!value) return;
    const i = value.indexOf('~');
    if (i === value.length - 1) row[colDef.name] = value + value.substr(0, value.length - 1);
}

//Show an editor for a single value of any type and maintains its value in row[colDef.name]; it also maintains
//the invalid message, for example for column 'firstName' the invalid message is in row['firstName$v'].
//This is used for both row values within persistons and criset values within viewon criteria.
//props.tableDef is the TableDefResponse which is the metadata for props.row
//props.colDef is the ColDefResponse (metadata for the column)
//props.row is the row object being edited (persiston row or criset)
//props.width is the css width string
//props.isCriterion is true for criterion, false for persiston value
//props.session is the retrodry Session 
//props.layer is the optional DatonStackState layer data for the containing stack (can be omitted if this is used outside a stack)
//props.onChanged is called with no arguments after a change is saved to the row (on each keystroke)
export default (props) => {
    const {colDef, row, width, layer, session, isCriterion} = props;
    const invalidMemberName = getInvalidMemberName(colDef);
    const [hasFocus, setHasFocus] = useState(false);
    const [valueAtFocus, setValueAtFocus] = useState(null);
    const [selectSource, setSelectSource] = useState(null); //only for select-type inputs; the array of rows to select from
    const [selectValueCol, setSelectValueCol] = useState(null); //only for select-type inputs; the column name of the value (primary key)
    const [selectDisplayCols, setSelectDisplayCols] = useState([]); //only for select-type inputs; the column names of the display values
    const [, setInvalidMessage] = useState(row[invalidMemberName]);
    let containerClass = row[invalidMemberName] ? 'invalid inputwrap' : 'valid inputwrap';
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
    const numberRangeChanged = (lo, hi) => { //accepts string entries
        let value = null;
        if (lo && hi) {
            if (lo === hi) value = lo;
            else value = lo + '~' + hi;
        }
        else if (lo) value = lo + '~';
        else if (hi) value = '~' + hi;
        row[colDef.name] = value;
        props.onChanged();
    };
    const stringChanged = (ev) => {
        row[colDef.name] = ev.target.value;
        props.onChanged();
    };
    const selectChanged = (ev) => {
        if (baseType === 'string') 
            stringChanged(ev);
        else if (isCriterion)
            numberRangeChanged(ev.target.value, ev.target.value);
        else 
            numberChanged(ev);
    };
    const afterEntryProcessed = ([msg, anyCascades]) => {
        setInvalidMessage(msg);
        if (anyCascades && layer) layer.stackstate.callOnChanged(); 
    };
    const ctrlBlurred = async (isLoOfRange) => {
        setHasFocus(false);

        //special case: if user entered lo and hi is not set, then set hi to the same
        if (isLoOfRange) autoPopulateHiFromLo(row, colDef);

        //validate on tab-out if any change
        if (valueAtFocus !== row[colDef.name]) {
            if (isNumericBaseType(baseType)) {
                if (isCriterion )
                    await processNumberRangeEntry(session, props.tableDef, colDef, baseType, row, invalidMemberName).then(afterEntryProcessed);
                else 
                    await processNumberEntry(session, props.tableDef, colDef, baseType, row, invalidMemberName).then(afterEntryProcessed);
            } else if (baseType === 'string') {
                await processStringEntry(session, props.tableDef, colDef, row, invalidMemberName).then(afterEntryProcessed);
            }
        }
    };
    const startLookup = () => {
        layer.stackstate.startLookup(layer, props.tableDef, row, colDef);
    };

    //determine from metadata if this is a dropdown select
    if (!selectSource && !colDef.lookupViewonTypeName && colDef.foreignKeyDatonTypeName) {
        const selectDef = session.getDatonDef(colDef.foreignKeyDatonTypeName);
        if (selectDef && selectDef.multipleMainRows) {
            session.get(colDef.foreignKeyDatonTypeName + '|+', {doSubscribeEdit: true}).then(d => {
                setSelectSource(d[selectDef.mainTableDef.name]); //this is the array of rows, not the top level of the whole-table persiston
                setSelectValueCol(selectDef.mainTableDef.primaryKeyColName);
                setSelectDisplayCols(getDropdownColumns(selectDef.mainTableDef));
            }); 
        }
    }

    //setup components that appear before/after the main control
    const popupError = <EditorPopupError show={hasFocus} message={row[invalidMemberName]}/>;
    let lookupButton = null;
    if (colDef.lookupViewonTypeName && layer) {
        lookupButton = <button className="btn-lookup" onClick={startLookup}>..</button>;
        wrapStyle.width = 'calc(' + wrapStyle.width + ' - 20px'; //for example changes '50%' to 'calc(50% - 20px)'
        containerClass += ' has-btn';
    }

    //setup main input control
    let inputCtrl = null;
    if (selectSource && selectValueCol) {
        inputCtrl = <select value={row[colDef.name] || ''} onChange={selectChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}>
            <option key="-1" value={null}></option>
            {selectSource.map(r => <option key={r[selectValueCol]} value={r[selectValueCol]}>{r[selectDisplayCols[0]]}</option>)}
        </select>;
        //todo show all display cols
    } 
    else if (baseType === 'bool') {
        if (isCriterion)
            inputCtrl = <select value={row[colDef.name] || ''} onChange={selectChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}>
                <option key="-1" value="">Any</option>
                <option key="1" value="1">Yes</option>
                <option key="0" value="0">No</option>
            </select>;
        else
            inputCtrl = <input type="checkbox" checked={row[colDef.name]} onChange={boolChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>;
    }
    else if (isNumericBaseType(baseType)) {
        if (isCriterion) {
            const loHi = splitOnTilde(row[colDef.name] || '');
            const loNumberChanged = (ev) => {
                numberRangeChanged(ev.target.value, loHi[1]);
            };
            const hiNumberChanged = (ev) => {
                numberRangeChanged(loHi[0], ev.target.value);
            };
            inputCtrl = <>
                <input className="criterion number" type="number" value={loHi[0] || ''} onChange={loNumberChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(true)}/> - &nbsp;
                <input className="criterion number" type="number" value={loHi[1] || ''} onChange={hiNumberChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>
            </>;
        }
        else
            inputCtrl = <input type="number" value={row[colDef.name] || ''} onChange={numberChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>;
    }
    else if (baseType === 'string') {
        //string renders multiline control if max length is > 200
        if (isCriterion)
            inputCtrl = <input className="criterion" value={row[colDef.name] || ''} onChange={stringChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)} />;
        else if (colDef.maxLength > 200) {
            inputCtrl = <textarea value={row[colDef.name] || ''} onChange={stringChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)} />;
        } else {
            inputCtrl = <input value={row[colDef.name] || ''} onChange={stringChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)} />;
        }
    }
    else if (baseType === 'date') { //todo
        inputCtrl = <input value={row[colDef.name] || ''} onChange={stringChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>;
    }
    else if (baseType === 'datetime') { //todo
        inputCtrl = <input value={row[colDef.name] || ''} onChange={stringChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>;
    }

    return (
        <span className={containerClass} style={wrapStyle}>
            {popupError}
            {inputCtrl}
            {lookupButton}
        </span>);
};
