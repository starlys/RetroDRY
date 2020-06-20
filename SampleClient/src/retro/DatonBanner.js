import React, {useState} from 'react';
import {securityUtil} from 'retrodry';

//displays banner in datonstack for a daton
//props.datonDef is the metadata for the daton
//props.editState is one of -1:wait icon; 0:not editable; 1:editable/view mode; 2:editing
//props.editClicked is the handler for edit button
//props.saveClicked is the handler for save button
//props.cancelClicked is the handler for cancel button
//props.removeClicked is the handler for remove button (meaning remove daton from stack)
//props.deleteClicked is the handler for deleting a persiston (only called after this component handles confirmation)
//props.parsedDatonKey
export default (props) => {
    const {datonDef, editState, parsedDatonKey} = props;
    const [isDeleteConfirming, setDeleteConfirming] = useState(false);

    //event handlers
    const deleteStarted = () => {
        setDeleteConfirming(true);
    };
    const deleteCanceled = () => {
        setDeleteConfirming(false);
    };

    //title text
    let title = datonDef.mainTableDef.prompt;
    if (parsedDatonKey.isPersiston()) {
        if (parsedDatonKey.isNew())
            title += ' - New';
        else {
            const readableKey = parsedDatonKey.persistonKeyAsString();
            if (readableKey) title += ' - ' + readableKey;
        }
    } else {
        title = 'Query: ' + title;
    }

    //todo language
    const allowDelete = !datonDef.multipleMainRows && securityUtil.canDeletePersiston(datonDef);
    return (
        <div className="daton-banner">
            <div className="right">
                {(editState === 2 && !isDeleteConfirming && allowDelete) && 
                    <button className="btn-delete-row" onClick={deleteStarted}>X</button>
                }
                {(editState === 2 && isDeleteConfirming) && <>
                    <span>Really delete {title}?</span>
                    <button className="btn-delete-row" onClick={props.deleteClicked}>Delete</button>
                    <button onClick={deleteCanceled}>Cancel</button>
                </>}
            </div>
            {editState !== 2 && <button onClick={props.removeClicked}> X </button>}
            {title}
            {editState === -1 && <button>Working...</button>}
            {editState === 1 && <button onClick={props.editClicked}>Edit</button>}
            {editState === 2 && <button onClick={props.saveClicked}>Save</button>}
            {editState === 2 && <button onClick={props.cancelClicked}>Cancel</button>}
        </div>
    );

};