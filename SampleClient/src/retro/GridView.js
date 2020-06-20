import React, {useState, useReducer} from 'react';
import DisplayValue from './DisplayValue';
import CardView from './CardView';

//Displays all rows of a daton table in grid format
//props.session is the session for obtaining layouts
//props.rows is an array of daton table rows
//props.datonDef is the DatonDefResponse (metadat for whole daton)
//props.tableDef is the TableDefResponse which is the metadata for props.rows
//props.edit is true to allow editing of child cards and deleting rows
//props.layer is the optional DatonStackState layer data for the containing stack (can be omitted if this is used outside a stack)
export default props => {
    const {rows, datonDef, tableDef, edit, session, layer} = props;
    const [expandRowIdx, setExpandRowIdx] = useState(-1);
    const [gridLayout, setGridLayout] = useState(null);
    const [, incrementRenderCount] = useReducer(x => x + 1, 0); //for certain cases to force rerender of grid

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
    const deleteRow = (ev, idx) => {
        ev.stopPropagation();
        rows.splice(idx, 1);
        setExpandRowIdx(-1);
        incrementRenderCount();
    }

    const children = [];
    const colSpan = colInfos.length + (edit ? 2 : 1);
    for (let idx = 0; idx < rows.length; ++idx) {
        const row = rows[idx];
        if (expandRowIdx === idx) {
            children.push(
                <tr key={idx} style={{height: '12px'}} onClick={() => clickRow(idx)}>
                    {edit && <td style={{width: '1em'}} key="del"><button className="btn-delete-row" onClick={(ev) => deleteRow(ev, idx)}>X</button></td>}
                    <td colSpan={colSpan - 1}></td>
                </tr>
            );
            children.push(
                <tr key={'expand' + idx}>
                    <td className="card-in-grid" colSpan={colSpan}><CardView session={session} edit={edit} row={row} 
                    datonDef={datonDef} tableDef={tableDef} layer={layer}/></td>
                </tr>
            );
        } else
            children.push(
                <tr key={idx} onClick={() => clickRow(idx)}>
                    {edit && <td style={{width: '1em'}} key="del"><button className="btn-delete-row" onClick={(ev) => deleteRow(ev, idx)}>X</button></td>}
                    {colInfos.map((ci, idx2) => {
                        const outValue = <DisplayValue session={session} colDef={ci.colDef} row={row} />;
                        let cellContent = outValue;
                        const isClickable = layer && ci.colDef.foreignKeyDatonTypeName; 
                        if (isClickable)
                            cellContent = <span className="grid-fk" onClick={e => {layer.stackstate.gridKeyClicked(e, layer, tableDef, row, ci.colDef); setExpandRowIdx(-1);}}>{cellContent}</span>;
                        return <td key={idx2}>{cellContent}</td>;
                    })}
                </tr>
            );
    }

    return (
        <>
            {rows.length > 0 && 
                <>
                    <div className="grid-banner">{tableDef.prompt}</div>
                    <table className="grid">
                        <thead>
                            <tr>
                                {edit && <th></th>}
                                {colInfos.map((ci, idx) => 
                                    <th key={idx} style={{width: ci.width + 'em'}}>{ci.colDef.prompt}</th>
                                )}
                            </tr>
                        </thead>
                        <tbody>
                            {children}
                        </tbody>
                    </table>
                </>
            }
            {edit && 
                <div>
                    <button onClick={addRow}> + {tableDef.prompt} </button>
                </div>
            }
        </>
    );
};