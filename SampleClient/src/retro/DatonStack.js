import React, { useState, useReducer } from 'react';
import DatonView from './DatonView';

//View/edit a stack of datons supporting some interoperations between them
//props.session is the session for obtaining layouts
//props.stackstate is an instance of DatonStackState which will track state for the mounted lifetime of this stack
export default (props) => {
    const {session} = props;
    const [stackstate, setStackstate] = useState(null); 
    const [, setForceRender] = useReducer(x => x + 1, 0); 

    //initialize
    if (!stackstate) {
        setStackstate(props.stackstate);
        props.stackstate.initialize(
            session,
            setForceRender //statechanged callback from DatonStackState used to just rerender this stack
            );
    }

    if (!stackstate) return null; 

    //note that DatonView is memoized so it's probably performant to rerender the stack often
    return stackstate.layers.map(layer => {
        return <DatonView key={layer.datonKey} session={session} stackstate={stackstate} datonDef={layer.datonDef} daton={layer.daton} edit={layer.edit} />;
    });
};