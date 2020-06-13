import React, { useReducer } from 'react';
import DatonView from './DatonView';

//View/edit a stack of datons supporting some interoperations between them
//props.session is the session for obtaining layouts
//props.stackstate is an instance of DatonStackState which will track state for the mounted lifetime of this stack
export default (props) => {
    const {session, stackstate} = props;
    const [renderCount, incrementRenderCount] = useReducer(x => x + 1, 0); 

    //statechanged callback from DatonStackState used to just rerender this stack
    const forceRerender = () => {
        stackstate.rerenderCount++;
        incrementRenderCount();
    };

    //initialize
    if (renderCount === 0) {
        incrementRenderCount();
        props.stackstate.initialize(session, forceRerender);
        return null;
    }

    //note that DatonView is memoized so it's probably performant to rerender the stack often
    return stackstate.layers.map(layer => {
        return <DatonView key={layer.datonKey} session={session} layer={layer} datonDef={layer.datonDef} daton={layer.daton} edit={layer.edit} renderCount={renderCount} />;
    });
};