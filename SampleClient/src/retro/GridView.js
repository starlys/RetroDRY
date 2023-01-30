import React, {useState, useReducer} from 'react';
import DisplayValue from './DisplayValue';
import CardView from './CardView';
import {securityUtil, seedNewRow} from 'retrodryclient';

//Displays all rows of a daton table in grid format
//props.session is the session for obtaining layouts
//props.rows is an array of daton table rows
//props.datonDef is the DatonDefResponse (metadata for whole daton)
//props.tableDef is the TableDefResponse which is the metadata for props.rows
//props.edit is true to allow editing of child cards and deleting rows
//props.layer is the optional DatonStackState layer data for the containing stack (can be omitted if this is used outside a stack)
//props.sortClicked is falsy if sorting is not allowed here, or a function taking the column name
//props.overrideGrid is the GridLayout to use (if omitted, uses the default defined by the session)
const Component = props => {
    const {rows, datonDef, tableDef, edit, session, layer} = props;
    const [expandRowIdx, setExpandRowIdx] = useState(-1);
    const [gridLayout, setGridLayout] = useState(null);
    const [, incrementRenderCount] = useReducer(x => x + 1, 0); //for certain cases to force rerender of grid

    //get layout
    let localGridLayout = gridLayout;
    if (!gridLayout) {
        localGridLayout = props.overrideGrid;
        if (!localGridLayout) {
            let businessContext = layer ? layer.businessContext : '';
            localGridLayout = session.layouts.getGrid(datonDef.name, tableDef.name, businessContext);
        }
        setGridLayout(localGridLayout);
    }

    //build colInfos array from layout with properties: width, colDef
    const getColDef = (name) => tableDef.cols.find(c => c.name === name);
    const colInfos = localGridLayout.columns.map(gc => {
        return { width: gc.width, colDef: getColDef(gc.name) };
    });

    //event handlers
    const clickRow = (idx) => {
        if (idx === expandRowIdx) idx = -1;
        setExpandRowIdx(idx);
    };
    const addRow = () => {
        let row = {};
        seedNewRow(row, tableDef);
        securityUtil.markRowCreatedOnClient(row);
        rows.push(row);
        setExpandRowIdx(rows.length - 1);
    };
    const deleteRow = (ev, idx) => {
        ev.stopPropagation();
        rows.splice(idx, 1);
        setExpandRowIdx(-1);
        incrementRenderCount();
    }

    //build data rows
    const children = [];
    const includeSpaceForDeleteButton = edit;
    const colSpan = colInfos.length + (includeSpaceForDeleteButton ? 2 : 1);
    for (let idx = 0; idx < rows.length; ++idx) {
        const row = rows[idx];
        const allowDelete = edit && (securityUtil.isRowCreatedOnClient(row) || securityUtil.canDeleteRow(tableDef));
        if (expandRowIdx === idx) {
            children.push(
                <tr key={idx} style={{height: '12px'}} onClick={() => clickRow(idx)}>
                    {allowDelete && <td style={{width: '1em'}} key="del"><button className="btn-delete-row" onClick={(ev) => deleteRow(ev, idx)}>X</button></td>}
                    <td colSpan={colSpan - 1}></td>
                </tr>
            );
            children.push(
                <tr key={'expand' + idx}>
                    <td className="card-in-grid" colSpan={colSpan}>
                        <CardView session={session} edit={edit} row={row} datonDef={datonDef} tableDef={tableDef} layer={layer} showChildTables={true}/>
                    </td>
                </tr>
            );
        } else
            children.push(
                <tr key={idx} onClick={() => clickRow(idx)}>
                    {includeSpaceForDeleteButton && <td style={{width: '1em'}} key="del">
                        {allowDelete && <button className="btn-delete-row" onClick={(ev) => deleteRow(ev, idx)}>X</button>}
                    </td>}
                    {colInfos.map((ci, idx2) => {
                        const outValue = <DisplayValue session={session} colDef={ci.colDef} row={row} />;
                        let cellContent = outValue;
                        const isClickable = layer && tableDef.primaryKeyColName === ci.colDef.name && ci.colDef.foreignKeyDatonTypeName; 
                        if (isClickable)
                            cellContent = <span className="grid-fk" onClick={e => {layer.stackstate.gridKeyClicked(e, layer, tableDef, row, ci.colDef); setExpandRowIdx(-1);}}>
                                ( {cellContent} )
                            </span>;
                        return <td key={idx2}>{cellContent}</td>;
                    })}
                </tr>
            );
    }

    //build header cells
    const colHeaders = colInfos.map((ci, idx) => {
        let cellContent = ci.colDef.prompt;
        const clickable = props.sortClicked && ci.colDef.allowSort;
        if (clickable) cellContent = <span className="grid-sortable" onClick={() => props.sortClicked(ci.colDef.name)}>{cellContent}</span>;
        return <th key={idx} style={{width: ci.width + 'em'}}>{cellContent}</th>;
    });

    const lang = session.dataDictionary.messageConstants;
    return (
        <>
            {rows.length > 0 && 
                <>
                    <div className="grid-banner">{lang.NAVLIST} {tableDef.prompt}</div>
                    <div className="grid-wrap">
                        <table className="grid">
                            <thead>
                                <tr>
                                    {includeSpaceForDeleteButton && <th></th>}
                                    {colHeaders}
                                </tr>
                            </thead>
                            <tbody>
                                {children}
                            </tbody>
                        </table>
                    </div>
                </>
            }
            {(edit && securityUtil.canCreateRow(tableDef)) &&
                <div>
                    <button onClick={addRow}> + {tableDef.prompt} </button>
                </div>
            }
        </>
    );
};

export default Component;
