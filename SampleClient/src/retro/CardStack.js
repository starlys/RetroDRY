import React, { useState } from 'react';
import CardView from './CardView';

//Displays all rows of a daton table in card format
//props.session is the session for obtaining layouts
//props.rows is the rows in the daton to display
//props.datonDef is the DatonDefResponse (metadata for whole daton)
//props.tableDef is the TableDefResponse which is the metadata for props.rows
//props.edit is true to display with editors; false for read only
//props.layer is the optional DatonStackState layer data for the containing stack (can be omitted if this is used outside a stack)
const Component = props => {
    const {session, rows, datonDef, tableDef, edit, layer} = props;
    const [cardLayout, setCardLayout] = useState(null);

    //initialize
    let localLayout = cardLayout;
    if (!localLayout) {
        let businessContext = layer ? layer.businessContext : '';
        localLayout = session.layouts.getCard(datonDef.name, tableDef.name, businessContext);
        setCardLayout(localLayout);
    }

    return rows.map(row =>
        <>
            <CardView session={session} row={row} overrideCard={localLayout} datonDef={datonDef} tableDef={tableDef} edit={edit} layer={layer} showChildTables={true}/>
            <hr/>
        </>
    );
};

export default Component;
