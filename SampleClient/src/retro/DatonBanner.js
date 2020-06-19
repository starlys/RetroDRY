import React from 'react';

//displays banner in datonstack for a daton
//props.datonDef is the metadata for the daton
//props.editState is one of -1:wait icon; 0:not editable; 1:editable/view mode; 2:editing
//props.editClicked is the handler for edit button
//props.saveClicked is the handler for save button
//props.cancelClicked is the handler for cancel button
//props.removeClicked is the handler for remove button
//props.parsedDatonKey
export default (props) => {
    const {datonDef, editState, parsedDatonKey} = props;

    //title text
    let title = datonDef.mainTableDef.prompt;
    if (parsedDatonKey.isPersiston()) {
        title += ' - ';
        if (parsedDatonKey.isNew())
            title += 'New';
        else
            title += parsedDatonKey.persistonKeyAsString();
    } else {
        title = 'Query: ' + title;
    }

    //todo language
    return (
        <div className="daton-banner">
            {editState !== 2 && <button onClick={props.removeClicked}> X </button>}
            {title}
            {editState === -1 && <button>Working...</button>}
            {editState === 1 && <button onClick={props.editClicked}>Edit</button>}
            {editState === 2 && <button onClick={props.saveClicked}>Save</button>}
            {editState === 2 && <button onClick={props.cancelClicked}>Cancel</button>}
        </div>
    );

};