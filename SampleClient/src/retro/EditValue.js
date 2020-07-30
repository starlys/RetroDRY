import React, {useState, useReducer} from 'react';
import EditorPopupError from './EditorPopupError';
import {DropdownState, splitOnTilde, getInvalidMemberName, processNumberEntry, processNumberRangeEntry, processStringEntry, 
    processDateTimeEntry, inputDateToWire, inputDateTimeToWire, wireDateToInput, wireDateTimeToDateTimeInputs,
    getBaseType, isNumericBaseType} from 'retrodryclient';

//modify row so the hi range is the same as the lo, if hi wasn't set; example: changes '6~' to '6'
function autoPopulateHiFromLo(row, colDef) {
    const value = row[colDef.name];
    if (!value) return;
    const i = value.indexOf('~');
    if (i === value.length - 1) row[colDef.name] = value.substr(0, value.length - 1);
}

//get the formatted version of a numeric col value from the row, by using the name$s property of the row to store it
//Example: row['price$s'] stores what the user is typing while row['price'] stores the parsed number
function getFormattedNumber(row, colName, baseType) {
    let s = row[colName + '$s'];
    if (s) return s;
    if (baseType === 'decimal' || baseType === 'double') {
        let n = parseFloat(row[colName]);
        if (isNaN(n)) s = '';
        else s = n.toFixed(2);
    } else {
        let n = parseInt(row[colName]);
        if (isNaN(n)) s = '';
        else s = n.toString(); 
    }
    return s;
}

//Show an editor for a single value of any type and maintains its value in row[colDef.name]; it also maintains
//the invalid message, for example for column 'firstName' the invalid message is in row['firstName$v'].
//This is used for both row values within persistons and criset values within viewon criteria.
//props.tableDef is the TableDefResponse which is the metadata for props.row
//props.colDef is the ColDefResponse (metadata for the column)
//props.row is the row object being edited (persiston row or criset)
//props.width is the css width string (ignored for some control types)
//props.isCriterion is true for criterion, false for persiston value
//props.session is the retrodry Session 
//props.layer is the optional DatonStackState layer data for the containing stack (can be omitted if this is used outside a stack)
//props.onChanged is called with no arguments after a change is saved to the row (on each keystroke)
export default (props) => {
    const {colDef, row, width, layer, session, isCriterion} = props;
    const invalidMemberName = getInvalidMemberName(colDef);
    const [hasFocus, setHasFocus] = useState(false);
    const [valueAtFocus, setValueAtFocus] = useState(null);
    const [selectKind, setSelectKind] = useState(0); //0:unknown at first render; -1:nothing; 1:in process of being calculated; 2:use lookup button; 3:use dropdpown select
    const [dropdownState, setDropdownState] = useState(null); //DropdownState instance
    const [, setInvalidMessage] = useState(row[invalidMemberName]);
    const [, incrementDropdownRenderCount] = useReducer(x => x + 1, 0); 
    let containerClass = row[invalidMemberName] ? 'invalid inputwrap' : 'valid inputwrap';
    const wrapStyle = {width: width};
    const baseType = getBaseType(colDef.wireType);

    //event handlers
    const ctrlFocused = () => {
        setHasFocus(true);
        setValueAtFocus(row[colDef.name]);
    };
    const boolChanged = (ev) => {
        row[colDef.name] = ev.target.checked;
        props.onChanged();
    };
    const numberChanged = (ev) => {
        row[colDef.name + '$s'] = ev.target.value; //see getFormattedNumber
        row[colDef.name] = parseFloat(ev.target.value); //may be NaN
        //note this stores strings in the $s property while the focus is in the input
        props.onChanged();
    };
    const rangeChanged = (lo, hi) => { //accepts string entries; use for any range types
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
        else if (baseType === 'bool') { //critiron values -1,0,1
            if (ev.target.value === '-1') delete row[colDef.name];
            else row[colDef.name] = ev.target.value;
        } else if (isCriterion) //date or number ranges
            rangeChanged(ev.target.value, ev.target.value);
        else 
            numberChanged(ev);
    };
    const afterEntryProcessed = ([msg, anyCascades]) => {
        setInvalidMessage(msg);
        if (anyCascades && layer) {
            layer.rerender();
        }
    };
    const ctrlBlurred = async (isLoOfRange) => {
        setHasFocus(false);

        //special case: if user entered lo, and hi is not set, then set hi to the same
        if (isLoOfRange) autoPopulateHiFromLo(row, colDef);

        //validate on tab-out if any change
        if (valueAtFocus !== row[colDef.name]) {
            let processResult = null;
            if (isNumericBaseType(baseType)) {
                if (isCriterion )
                    processResult = await processNumberRangeEntry(session, props.tableDef, colDef, baseType, row, invalidMemberName);
                else 
                    processResult = await processNumberEntry(session, props.tableDef, colDef, baseType, row, invalidMemberName);
            } else if (baseType === 'string') {
                processResult = await processStringEntry(session, props.tableDef, colDef, row, invalidMemberName);
            } else if (baseType === 'date' || baseType === 'datetime') {
                processResult = await processDateTimeEntry(session, props.tableDef, colDef, row, invalidMemberName);
            }
            if (processResult) afterEntryProcessed(processResult);
        }
    };
    const startLookup = () => {
        layer.stackstate.startLookup(layer, props.tableDef, row, colDef);
    };

    //determine from metadata if this is a dropdown select or lookup using a separate viewon in the stack
    if (selectKind === 1) return null; //in process
    if (selectKind === 0) {
        const mightUseDDState = colDef.selectBehavior || colDef.foreignKeyDatonTypeName;
        if (mightUseDDState) {
            setSelectKind(1); //in process
            const ddstate = new DropdownState(session, row, colDef);
            setDropdownState(ddstate);
            ddstate.build().then(() => {
                if (ddstate.useLookupButton) setSelectKind(2);
                else if (ddstate.useDropdown) setSelectKind(3);
                else setSelectKind(-1);
            });
        }
        else setSelectKind(-1); //not using ddState

        //don't show anything while we load initial dropdown contents
        return null;
    }

    //setup components that appear before/after the main control
    const popupError = <EditorPopupError show={hasFocus} message={row[invalidMemberName]}/>;
    let lookupButton = null;
    if (selectKind === 2 && layer) { 
        lookupButton = <button className="btn-lookup" onClick={startLookup}>..</button>;
        containerClass += ' has-btn';
    }

    //setup main input control
    let inputCtrl = null;
    if (selectKind === 3) {
        dropdownState.build().then(anyChanges => {
            if (anyChanges) incrementDropdownRenderCount();
        });
        inputCtrl = <select value={row[colDef.name] || ''} onChange={selectChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}>
            <option key="-1" value={null}></option>
            {dropdownState.dropdownRows.map(r => <option key={r[dropdownState.dropdownValueCol]} value={r[dropdownState.dropdownValueCol]}>
                {dropdownState.dropdownDisplayCols.map(dcname => r[dcname]).join(' - ')}
            </option>)}
        </select>;
    } 
    else if (baseType === 'bool') {
        if (isCriterion)
            inputCtrl = <select value={row[colDef.name] || ''} onChange={selectChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}>
                <option key="-1" value="">Any</option>
                <option key="1" value="1">Yes</option>
                <option key="0" value="0">No</option>
            </select>;
        else {
            inputCtrl = <input type="checkbox" checked={row[colDef.name]} onChange={boolChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>;
            wrapStyle.width = '40px';
        }
    }
    else if (isNumericBaseType(baseType)) {
        if (isCriterion) {
            const loHi = splitOnTilde(row[colDef.name] || '');
            const loNumberChanged = (ev) => {
                rangeChanged(ev.target.value, loHi[1]);
            };
            const hiNumberChanged = (ev) => {
                rangeChanged(loHi[0], ev.target.value);
            };
            inputCtrl = <>
                <input className="criterion number" type="number" value={loHi[0] || ''} onChange={loNumberChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(true)}/> - &nbsp;
                <input className="criterion number" type="number" value={loHi[1] || ''} onChange={hiNumberChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>
            </>;
        }
        else {
            //the input will use the string version of the number for editing, which is in the row with $s appended.
            inputCtrl = <input type="number" value={getFormattedNumber(row, colDef.name, baseType)} onChange={numberChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>;
        }
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
    else if (baseType === 'date') { 
        if (isCriterion) {
            const loHi = splitOnTilde(row[colDef.name] || '');
            const loDateChanged = (ev) => {
                rangeChanged(inputDateToWire(ev.target.value), loHi[1]);
            };
            const hiDateChanged = (ev) => {
                rangeChanged(loHi[0], inputDateToWire(ev.target.value));
            };
            inputCtrl = <>
                <input className="criterion date" type="date" value={wireDateToInput(loHi[0])} onChange={loDateChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(true)}/> - &nbsp;
                <input className="criterion date" type="date" value={wireDateToInput(loHi[1])} onChange={hiDateChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>
            </>;
        }
        else {
            const dateChanged = (ev) => {
                row[colDef.name] = inputDateToWire(ev.target.value);
                props.onChanged();
            };
            inputCtrl = <input type="date" value={wireDateToInput(row[colDef.name])} onChange={dateChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>;
        }
    }
    else if (baseType === 'datetime') { 
        if (isCriterion) {
            const loHi = splitOnTilde(row[colDef.name] || '');
            const [valLoDate, valLoTime] = wireDateTimeToDateTimeInputs(session, loHi[0]);
            const [valHiDate, valHiTime] = wireDateTimeToDateTimeInputs(session, loHi[1]);
            const loDateChanged = (ev) => {
                rangeChanged(inputDateTimeToWire(session, ev.target.value, valLoTime), loHi[1]);
            };
            const loTimeChanged = (ev) => {
                rangeChanged(inputDateTimeToWire(session, valLoDate, ev.target.value), loHi[1]);
            };
            const hiDateChanged = (ev) => {
                rangeChanged(loHi[0], inputDateTimeToWire(session, ev.target.value, valHiTime));
            };
            const hiTimeChanged = (ev) => {
                rangeChanged(loHi[0], inputDateTimeToWire(session, valHiDate, ev.target.value));
            };
            inputCtrl = <>
                <input className="criterion date" type="date" value={valLoDate} onChange={loDateChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>
                <input className="criterion date" type="time" value={valLoTime} onChange={loTimeChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(true)}/> - &nbsp;
                <input className="criterion date" type="date" value={valHiDate} onChange={hiDateChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>
                <input className="criterion date" type="time" value={valHiTime} onChange={hiTimeChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>
            </>;
        }
        else {
            const [valDate, valTime] = wireDateTimeToDateTimeInputs(session, row[colDef.name]);
            const dateChanged = (ev) => {
                row[colDef.name] = inputDateTimeToWire(session, ev.target.value, valTime);
                props.onChanged();
            };
            const timeChanged = (ev) => {
                row[colDef.name] = inputDateTimeToWire(session, valDate, ev.target.value);
                props.onChanged();
            };
            inputCtrl = <>
                <input className="date" type="date" value={valDate} onChange={dateChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>
                <input className="date" type="time" value={valTime} onChange={timeChanged} onFocus={ctrlFocused} onBlur={() => ctrlBlurred(false)}/>
            </>;
        }
    }

    return (
        <span className={containerClass} style={wrapStyle}>
            {popupError}
            {inputCtrl}
            {lookupButton}
        </span>);
};
