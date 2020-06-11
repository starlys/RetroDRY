import React, { useState } from 'react';
import CardView from './CardView';

//Displays all rows of a daton table in card format
//props.session is the session for obtaining layouts
//props.rows is the rows in the daton to display
//props.datonDef is the DatonDefResponse (metadata for whole daton)
//props.tableDef is the TableDefResponse which is the metadata for props.rows
//props.edit is true to display with editors; false for read only
export default props => {
    const {session, rows, datonDef, tableDef, edit} = props;
    const [cardLayout, setCardLayout] = useState(null);

    //initialize
    let localLayout = cardLayout;
    if (!localLayout) {
        localLayout = session.getCardLayout(datonDef.name, tableDef.name);
        setCardLayout(localLayout);
    }

    return rows.map(row =>
        <>
            <CardView session={session} row={row} nestCard={localLayout} datonDef={datonDef} tableDef={tableDef} edit={edit} />
            <hr/>
        </>
    );
};
