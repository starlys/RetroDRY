import React, { useState, useReducer } from 'react';
import DisplayValue from './DisplayValue';
import CardView from './CardView';
import GridView from './GridView';
import EditValue from './EditValue';
import {securityUtil} from 'retrodryclient';

//get width in em units for a colDef
//forcedWidth is optional string width specified in layout; if missing it uses the colun type and length
function widthByType(colDef, forcedWidth) {
    if (forcedWidth) {
        const w2 = parseInt(forcedWidth);
        if (w2 && !isNaN(w2)) return w2;
    } 
    if (colDef.wireType === 'string' || colDef.wireType === 'nstring') return Math.max(8, Math.min(50, 0.8 * (colDef.maxLength || 50)))
    if (colDef.wireType.indexOf('datetime') >= 0) return 20;
    return 12;
}

//determine if column is editable in this row (row can be falsy if criteria editing)
function isEditable(row, colDef) {
    if (colDef.isComputed || colDef.leftJoin) return false;
    if (row && securityUtil.isRowCreatedOnClient(row))
        return securityUtil.canEditColDefInNewRow(colDef);
    return securityUtil.canEditColDef(colDef);
}

//Displays one row of a daton table in card format, or one criteria set
//props.session is the session for obtaining layouts
//props.row is the row in the daton to display (null/missing for criteria)
//props.criset is the criteria set to display (null/missing for rows)
//props.overrideCard is the PanelLayout to use (if omitted, uses the default defined by the session)
//props.datonDef is the DatonDefResponse (metadata for whole daton)
//props.tableDef is the TableDefResponse which is the metadata for props.row
//props.edit is true to display with editors; false for read only (ignored for criteria)
//props.layer is the optional DatonStackState layer data for the containing stack (can be omitted if this is used outside a stack)
//props.showChildTables is true if you want to show child tables (the default when shown in a stack)
//props.isNested is true for nested CardViews; should be omitted from user code
export default props => {
    const {session, overrideCard, row, criset, datonDef, tableDef, edit, layer, isNested, showChildTables} = props;
    const [cardLayout, setCardLayout] = useState(null);
    const [, incrementCardRenderCount] = useReducer(x => x + 1, 0); 

    //determine layout
    let card = overrideCard;
    if (!card) {
        card = cardLayout;
        if (!card) {
            let businessContext = layer ? layer.businessContext : '';
            card = session.layouts.getCard(datonDef.name, tableDef.name, businessContext);
            setCardLayout(card);
        }
        if (!card) return null;
    }

    let cardClass = 'card ' + (card.label ? 'border' : 'no-border');
    if (card.classNames) cardClass += ' ' + card.classNames;
    let maxWidth = 0;
    const isCriteria = !row;

    const children = card.content.map((item, idx1) => {
        let child = null;
        let totalWidth = 0;

        //if item is one or more colum names, set child to the prompt and display value(s)
        if (typeof item === 'string') {
            const colNamesAndWidths = item.split(' ');
            const colInfos = []; //members are {colDef, emw, width} where emw is numeric width is in em units, and width is css string
            for (let colNameAndWidth of colNamesAndWidths) {
                let [colName, width] = colNameAndWidth.split(':'); //width can be missing
                const colDef = tableDef.cols.find(c => c.name === colName);
                if (colDef) {
                    width = widthByType(colDef, width);
                    totalWidth += width;
                    colInfos.push({colDef: colDef, emw: width});
                }
            }
            for (let c of colInfos) c.width = ((c.emw / totalWidth) * 100) + '%';
            if (colInfos.length) {
                let cells = [];
                for (let idx2 = 0; idx2 < colInfos.length; ++idx2) {
                    const c = colInfos[idx2];
                    const editableCol = isEditable(row, c.colDef);
                    if (isCriteria || (edit && editableCol))
                        cells.push(<EditValue key={idx2} tableDef={tableDef} colDef={c.colDef} isCriterion={isCriteria}
                            row={row || criset} width={c.width} session={session} layer={layer} onChanged={incrementCardRenderCount}/>);
                    else
                        cells.push(<DisplayValue key={idx2} session={session} colDef={c.colDef} row={row || criset} width={c.width} />);
                }
                child = <>
                    <span className="card-label">{colInfos[0].colDef.prompt}</span>
                    {cells}
                </>;
            }
        } 

        //if item is an injected function...
        else if (typeof item === 'function') {
            child = item(row || criset, edit, layer);
        }
        
        //if item is a nested panel, recur
        else if (item.content) {
            child = <CardView session={session} edit={edit} row={row} criset={criset} overrideCard={item} datonDef={datonDef} tableDef={tableDef} 
                layer={layer} isNested={true} />
        }

        maxWidth = Math.max(maxWidth, totalWidth);
        const divClass = card.horizontal ? 'card-horz' : 'card-vert';
        const divStyle = {};
        if (!isCriteria) {
            divStyle.maxWidth = '100%';
            if (totalWidth) divStyle.width = totalWidth + 'em';
        }
        return <div key={idx1} className={divClass} style={divStyle}>{child}</div>;
    });

    //add GridView elements for each child table
    let childGridElements = null;
    if (showChildTables && !isNested && !isCriteria && tableDef.children) {
        childGridElements = [];
        for (let i = 0; i < tableDef.children.length; ++i) {
            const childTableDef = tableDef.children[i];
            let childRows = row[childTableDef.name];
            if (!childRows) {
                childRows = [];
                row[childTableDef.name] = childRows;
            }
            if (edit || childRows.length)
                childGridElements.push(<GridView key={'g' + i} session={session} rows={childRows} datonDef={datonDef} tableDef={childTableDef} 
                    edit={edit} layer={layer} />);
        }
        childGridElements.push(<hr key={'hr_end'} />);
    }

    return (
        <>
            <fieldset className={cardClass}>
                {card.label && <legend>{card.label}</legend>}
                {children}
            </fieldset>
            {childGridElements}
        </>
    );
};