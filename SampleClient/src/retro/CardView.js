import React, { useState } from 'react';
import DisplayValue from './DisplayValue';
import CardView from './CardView';
import GridView from './GridView';
import EditValue from './EditValue';
import EditCriterion from './EditCriterion';

//get width in em units for a colDef
function widthByType(colDef) {
    if (colDef.wireType === 'string' || colDef.wireType === 'nstring') return Math.max(8, Math.min(50, 0.8 * (colDef.maxLength || 50)))
    if (colDef.wireType === 'bool' || colDef.wireType === 'nbool') return 3;
    if (colDef.wireType.indexOf('date') >= 0) return 12;
    return 6;
}

//Displays one row of a daton table in card format, or one criteria set
//props.session is the session for obtaining layouts
//props.row is the row in the daton to display (null/missing for criteria)
//props.criset is the criteria set to display (null/missing for rows)
//props.nestCard is the PanelLayout to use (only when nested; omit this from the top level call)
//props.datonDef is the DatonDefResponse (metadata for whole daton)
//props.tableDef is the TableDefResponse which is the metadata for props.row
//props.edit is true to display with editors; false for read only (ignored for criteria)
//props.rerenderCode is a string that changes as a way to  force rerender of memoized EditValues
//props.layer is the optional DatonStackState layer data for the containing stack (can be omitted if this is used outside a stack)
export default props => {
    const {session, nestCard, row, criset, datonDef, tableDef, edit, layer} = props;
    const [cardLayout, setCardLayout] = useState(null);

    //determine if top level or nested, and get top layout
    let card = nestCard;
    const isTopLevel = !nestCard;
    if (!card) {
        card = cardLayout;
        if (!card) {
            card = session.getCardLayout(datonDef.name, tableDef.name);
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
            const colNames = item.split(' ');
            const colDefs = []; //members are {colDef, emw, width} where emw is numeric width is in em units, and width is css string
            for (let colName of colNames) {
                const colDef = tableDef.cols.find(c => c.name === colName);
                if (colDef) {
                    let width = widthByType(colDef);
                    totalWidth += width;
                    colDefs.push({colDef: colDef, emw: width});
                }
            }
            for (let c of colDefs) c.width = ((c.emw / totalWidth) * 100) + '%';
            if (colDefs.length) {
                let cells;
                if (isCriteria)
                    cells = colDefs.map((c, idx2) => <EditCriterion key={idx2} colDef={c.colDef} criset={criset} />);
                else if (edit)
                    cells = colDefs.map((c, idx2) => <EditValue key={'_' + idx2 + '_' + props.rerenderCode} tableDef={tableDef} colDef={c.colDef} row={row} width={c.width} layer={layer} />);
                else //display row
                    cells = colDefs.map((c, idx2) => <DisplayValue key={idx2} colDef={c.colDef} row={row} width={c.width} />);
                child = <>
                        <span className="card-label">{colDefs[0].colDef.prompt}</span>
                        {cells}
                    </>;
            }
        } 
        
        //if item is a nested panel, recur
        else if (item.content) {
            child = <CardView session={session} edit={edit} row={row} criset={criset} nestCard={item} datonDef={datonDef} tableDef={tableDef} 
                layer={layer} rerenderCode={props.rerenderCode} />
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

    let childGridElements = null;
    if (isTopLevel && !isCriteria && tableDef.children) {
        childGridElements = [];
        for (let i = 0; i < tableDef.children.length; ++i) {
            const childTableDef = tableDef.children[i];
            const childRows = row[childTableDef.name] || [];
            childGridElements.push(<hr key={'hr' + i} />);
            childGridElements.push(<GridView key={'g' + i} session={session} rows={childRows} datonDef={datonDef} tableDef={childTableDef} edit={edit} />);
        }
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