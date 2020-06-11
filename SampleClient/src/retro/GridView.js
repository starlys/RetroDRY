import React, {useState} from 'react';
import DisplayValue from './DisplayValue';
import CardView from './CardView';

//Displays all rows of a daton table in grid format
//props.session is the session for obtaining layouts
//props.rows is an array of daton table rows
//props.datonDef is the DatonDefResponse (metadat for whole daton)
//props.tableDef is the TableDefResponse which is the metadata for props.rows
//props.edit is true to allow editing of child cards
export default props => {
    const {rows, datonDef, tableDef, edit, session} = props;
    const [expandRowIdx, setExpandRowIdx] = useState(-1);
    const [gridLayout, setGridLayout] = useState(null);

    //get layout
    let localGridLayout = gridLayout;
    if (!gridLayout) {
        localGridLayout = session.getGridLayout(datonDef.name, tableDef.name);
        setGridLayout(localGridLayout);
    }

    const getColDef = (name) => tableDef.cols.find(c => c.name === name);
    const colInfos = localGridLayout.columns.map(gc => {
        return { width: gc.width, colDef: getColDef(gc.name) };
    });

    //events
    const clickRow = (idx) => {
        if (idx === expandRowIdx) idx = -1;
        setExpandRowIdx(idx);
    };
    const addRow = () => {
        rows.push({});
        setExpandRowIdx(rows.length - 1);
    };

    const children = [];
    for (let idx = 0; idx < rows.length; ++idx) {
        const row = rows[idx];
        children.push(
            <tr key={idx} onClick={() => clickRow(idx)}>
                {colInfos.map((ci, idx2) =>
                    <td key={idx2}><DisplayValue colDef={ci.colDef} row={row} /></td>
                )}
            </tr>
        );
        if (expandRowIdx === idx)
            children.push(<tr><td className="card-in-grid" colspan={colInfos.length + 1}><CardView session={session} edit={edit} row={row} datonDef={datonDef} tableDef={tableDef} /></td></tr>);
    }

    return (
        <>
            <table className="grid">
                <thead>
                    <tr>
                        {colInfos.map((ci, idx) => 
                            <th key={idx} style={{width: ci.width + 'em'}}>{ci.colDef.prompt}</th>
                        )}
                    </tr>
                </thead>
                <tbody>
                    {children}
                </tbody>
            </table>
            {edit && 
                <div>
                    <button onClick={addRow}> + </button>
                </div>
            }
        </>
    );
};