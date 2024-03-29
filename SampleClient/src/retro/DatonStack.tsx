import React, { useReducer } from 'react';
import { Session } from 'retrodryclient';
import DatonStackState from './DatonStackState';
import DatonView from './DatonView';

//View/edit a stack of datons supporting some interoperations between them
//props.session is the session for obtaining layouts
//props.stackstate is an instance of DatonStackState which will track state for the mounted lifetime of this stack
interface TProps {
    session: Session;
    stackstate: DatonStackState;
}
const Component = (props: TProps) => {
    const {session, stackstate} = props;
    const [stackRenderCount, incrementStackRenderCount] = useReducer(x => x + 1, 0); 

    //statechanged callback from DatonStackState used to just rerender this stack;
    //but note the datons are memoized so rerendering them or remounting them uses a different technique
    const forceRerender = () => {
        incrementStackRenderCount();
    };

    //initialize
    if (stackRenderCount === 0) {
        incrementStackRenderCount();
        props.stackstate.initialize(session, forceRerender);
        return null;
    }

    //note that DatonView is memoized so it's probably performant to rerender the stack often
    return <>
        {stackstate.layers.map(layer => 
            <DatonView key={layer.datonKey + ':' + layer.mountCount} session={session} layer={layer} datonDef={layer.datonDef} daton={layer.daton} 
            edit={layer.edit} renderCount={layer.renderCount} />
        )}
        </>;
    ;
};

export default Component;
