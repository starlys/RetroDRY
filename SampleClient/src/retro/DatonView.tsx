import React, { useState, useRef, useEffect, ReactElement, DOMElement } from 'react';
import CardView from './CardView';
import GridView from './GridView';
import {TableRecurPointFromDaton, DatonKey, parseDatonKey, validateAll, validateCriteria, securityUtil, IdleTimer, TableDefResponse, DatonDefResponse, Session} from 'retrodryclient';
import DatonBanner from './DatonBanner';
import CardStack from './CardStack';
import { DatonStackLayer } from './DatonStackState';

//given the old viewon key and criset (object indexed by coldef names, containing strings), build a new key
function buildViewonKey(oldKey: string, tableDef: TableDefResponse, criset: {[name: string]: string}) {
    const parsedOldKey = parseDatonKey(oldKey);
    const segments: string[] = [];
    for (let colDef of tableDef.cols) {
        let criValue = criset[colDef.name];
        if (!criValue) continue;
        let name = colDef.name;
        name = name[0].toUpperCase() + name.substr(1);
        segments.push(name + '=' + criValue);
    }
    return new DatonKey(parsedOldKey.typeName, segments).toKeyString();
}

//given the string version of a viewon key, return a criset object indexed by critierion name
function unpackViewonKey(key: string): {[name: string]: string} {
    const ret:{[name:string]:string} = {};
    const parsedKey = parseDatonKey(key);
    for (let segment of parsedKey.otherSegments) {
        const eq = segment.indexOf('=');
        if (eq > 0) {
            let name = segment.substring(0, eq);
            name = name[0].toLowerCase() + name.substring(1);
            ret[name] = segment.substring(eq + 1);
        }
    }
    return ret;
}

//create panel with buttons to load each page up to the current one; currentPageNo may be NaN or falsy if we are on page 0
//layer must be set here.
function buildTopPagingButtons(layer: DatonStackLayer, currentPageNo: number, allowNext: boolean) {
    if (isNaN(currentPageNo) || !currentPageNo) currentPageNo = 0;
    const buttons: ReactElement[] = [];
    const lang = layer.stackstate.session?.dataDictionary?.messageConstants ?? {};
    for (let i = 0; i < currentPageNo; ++i) buttons.push(
        <button key={i} onClick={() => layer.stackstate.goToPage(layer, i)}> {i + 1} </button>
    );
    buttons.push(<button key={currentPageNo} disabled={true} className="current"> {currentPageNo + 1} </button>);
    if (allowNext) buttons.push(<button key="next" onClick={() => layer.stackstate.goToPage(layer, currentPageNo + 1)}> &gt;&gt; </button>);
    return <div className="page-select-panel">{lang.NAVPAGE} {buttons}</div>;
}

//Displays or edits one daton; also scrolls to it
//props.datonDef is the daton definition (DatonDefResponse)
//props.daton is the daton (non-editable version)
//props.session is the session for obtaining layouts
//props.edit is true to display initially with editors; false for read only
//props.layer is the optional DatonStackState layer data for the containing stack (can be omitted if this is used outside a stack)
//props.renderCount is a number incremented by the caller to force rerender when no other props changed
interface TProps {
    datonDef: DatonDefResponse;
    session: Session;
    layer: DatonStackLayer;
    edit: boolean;
    daton: any;
    renderCount?: number;
}
export default React.memo((props: TProps) => {
    const {datonDef, session, layer} = props;
    const [topStyle, setTopStyle] = useState<string|null>(null); //'c' or 'g' for card or grid; null on first render
    const [parsedDatonKey, setParsedDatonKey] = useState<DatonKey|null>(null); 
    const [isEditing, setIsEditing] = useState(props.edit); //note we have to set layer.edit whenever we set this so other controls know the mode
    const [isWorking, setIsWorking] = useState(false);
    const [daton, setDaton] = useState(props.daton);
    const [errorItems, setErrorItems] = useState<string[]>([]); //array of strings to display as errors
    const idleTimer = useRef<IdleTimer|null>(null);
    const criset = useRef<{[name: string]: string}>({}); //the viewon key segments in unpacked object form (includes _sort and _page)
    const domElement = useRef<any>();
    const validationCount = useRef(0); //used to force rerender of EditValues after validation is run

    //scroll on first render
    useEffect(() => {
        if (domElement && domElement.current) 
            domElement.current.parentElement.scrollTo(0, domElement.current.offsetTop);
    }, [domElement]);

    //clean up idle timer
    const cancelIdleTimer = () => {
        if (idleTimer.current) idleTimer.current.stop();
    };
    useEffect(() => {
        return () => cancelIdleTimer();
    });

    //event handlers
    const doExport = () => {
        props.session.exportAsCsv(daton.key);
    };
    const editingNearingTimeout = () => {
        const lang = session.dataDictionary?.messageConstants ?? {};
        setErrorItems([lang.INFOTIMEOUT]);
    };
    const editingNotNearingTimeout = () => {
        setErrorItems([]); 
    };
    const editingTimedOut = () => {
        cancelClicked(); 
    };
    const cancelClicked = async () => {
        setIsEditing(false);
        cancelIdleTimer();
        setErrorItems([]);
        if (layer) layer.edit = false;
        const isNew = parsedDatonKey?.isNew();
        if (isNew)
            layer.stackstate.removeByKey(daton.key, false);
        else {
            try {
                await session.changeSubscribeState([daton], 1);
                const d = await session.get(daton.key, {forceCheckVersion:true});
                setDaton(d);
            }
            catch {
                const lang = session.dataDictionary?.messageConstants ?? {};
                setErrorItems([lang.ERRNET]);
            }
        }
    };
    const editClicked = async () => {
        //note this can be called before a real render happened
        const isNew = parseDatonKey(daton.key).isNew();
        const lang = session.dataDictionary?.messageConstants ?? {};
        if (isNew) {
            setIsEditing(true);
            if (layer) layer.edit = true;
            return;
        }
        
        //get latest version if existing persiston
        setIsWorking(true);
        try {
            const d = await session.get(daton.key, {isForEdit:true, forceCheckVersion:true});
            setDaton(d);
            const errors = await session.changeSubscribeState([d], 2);
            setIsWorking(false);
            const myerrors = errors[d.key];
            if (myerrors) {
                setErrorItems([lang.ERRLOCK]); 
                setIsEditing(false);
                if (layer) layer.edit = false;
            } else {
                setIsEditing(true);
                if (!idleTimer.current) idleTimer.current = new IdleTimer();
                idleTimer.current.start(5 * 60, 6 * 60, editingNearingTimeout, editingNotNearingTimeout, editingTimedOut);
                if (layer) layer.edit = true;
                setErrorItems([]);
            }
        }
        catch {
            const lang = session.dataDictionary?.messageConstants ?? {};
            setErrorItems([lang.ERRNET]);
        }
    };
    const saveClicked = async () => {
        if (!parsedDatonKey) return;
        const isNew = parsedDatonKey.isNew();
        const lang = session.dataDictionary?.messageConstants ?? {};
        validationCount.current = validationCount.current + 1;

        //local errors
        const localErrors = validateAll(datonDef, daton);
        if (localErrors.length) {
            setErrorItems(localErrors);
            return;
        }

        //server attempt save and get errors
        setIsWorking(true);
        try {
            const saveInfo = await session.save([daton]);
            setIsWorking(false);
            if (saveInfo.success) {
                setIsEditing(false);
                cancelIdleTimer();
                if (layer) layer.edit = false;
                if (layer && layer.propagateSaveToViewon) layer.propagateSaveToViewon(daton);
                if (isNew) {
                    const newKey = saveInfo.details[0].newKey;
                    if (newKey) {
                        await layer.stackstate.replaceKey(daton.key, newKey, false);
                        //note that here, this DatonView instance is no longer mounted
                        if (layer.stackstate.onLayerSaved) layer.stackstate.onLayerSaved(newKey);
                    } else
                        layer.stackstate.removeByKey(daton.key, false);
                } else {
                    const loadResults = await session.getMulti([daton.key], {doSubscribe: true, forceCheckVersion: true});
                    const loadResult = loadResults[0];
                    if (loadResult.daton) {
                        layer.stackstate.replaceDaton(daton.key, loadResult.daton);
                    }
                    setErrorItems(loadResult.errors ?? []);
                    if (layer.stackstate.onLayerSaved) layer.stackstate.onLayerSaved(daton.key);
                }
            } else {
                let errors: string[] = [];
                if (saveInfo && saveInfo.details && saveInfo.details[0]) errors = saveInfo.details[0].errors ?? [];
                else errors.push(lang.ERRNET); 
                setErrorItems(errors);
            }
        }
        catch (e)  {
            console.log('Error in view:', e);
            setErrorItems([lang.ERRNET]);
        }
    };
    const removeClicked = () => {
        if (isEditing || !layer) return;
        layer.stackstate.removeByKey(daton.key, true);
    };
    const deleteClicked = async () => {
        setIsWorking(true);
        try {
            const saveInfo = await session.deletePersiston(daton);
            setIsWorking(false);
            if (saveInfo.success) {
                cancelIdleTimer();
                if (layer) layer.stackstate.removeByKey(daton.key, true);
            } else {
                const result = saveInfo.details[0];
                setErrorItems(result.errors || []);
            }
        }
        catch {
            const lang = session.dataDictionary?.messageConstants ?? {};
            setErrorItems([lang.ERRNET]);
        }
    };
    const doSearch = async () => {
        if (!layer || !datonDef.criteriaDef) return;

        //local errors
        const localErrors = validateCriteria(datonDef.criteriaDef, criset.current);
        if (localErrors.length) {
            setErrorItems(localErrors);
            return;
        }

        //load new viewon
        try {
            const newKey = buildViewonKey(daton.key, datonDef.criteriaDef, criset.current);
            const loadResults = await session.getMulti([newKey], {forceCheckVersion: true});
            const loadResult = loadResults[0];
            if (loadResult.daton) {
                //tell stack to replace this instance with a new daton view
                validationCount.current = validationCount.current + 1;
                layer.stackstate.replaceDaton(daton.key, loadResult.daton);
            } else {
                //show errors if server validation failed
                setErrorItems(loadResult.errors ?? []);
            }
        } catch (e) {
            const lang = session.dataDictionary?.messageConstants ?? {};
            setErrorItems([lang.ERRNET]);
        }
    };

    //first render: set some other state variables and return null
    const isFirstRender = !topStyle;
    if (isFirstRender) {
        //determine if grid or card: try using grid if possible and fall back to card
        setTopStyle('c');
        if (datonDef.multipleMainRows) {
            let businessContext = layer ? layer.businessContext : '';
            const mainGridLayout = session.layouts.getGrid(datonDef.name, datonDef.mainTableDef.name, businessContext);
            const mainCardLayout = session.layouts.getCard(datonDef.name, datonDef.mainTableDef.name, businessContext);
            if ((mainGridLayout && !mainGridLayout.isAutoGenerated) || !mainCardLayout || mainCardLayout.isAutoGenerated) {
                setTopStyle('g');
            }
        }

        //save parsed key
        setParsedDatonKey(parseDatonKey(daton.key));

        //set criset from viewon key
        if (datonDef.criteriaDef) criset.current = unpackViewonKey(daton.key);

        //optionally start editing 
        if (props.edit) editClicked();

        return null;
    }
    if (!parseDatonKey) return null;

    //set topContent to grid or card view of main table
    let topContent;
    if (topStyle === 'c') {
        if (datonDef.multipleMainRows) {
            const rt = TableRecurPointFromDaton(datonDef, daton); 
            topContent = <CardStack session={session} rows={rt.table} datonDef={datonDef} tableDef={datonDef.mainTableDef} edit={isEditing} layer={layer}/>;
        } else {
            topContent = <CardView session={session} row={daton} datonDef={datonDef} tableDef={datonDef.mainTableDef} edit={isEditing} 
                layer={layer} validationCount={validationCount.current} showChildTables={true} />;
        }
    } else { //grid
        if (datonDef.multipleMainRows) {
            const rt = TableRecurPointFromDaton(datonDef, daton); 
            let sortHandler: ((n: string) => void)|undefined = undefined;
            if (!datonDef.isPersiston && layer) sortHandler = colName => layer.stackstate.doSort(layer, colName);
            let topPagingButtons: ReactElement|null = null;
            if (layer && (criset.current._page || !daton.isComplete)) 
                topPagingButtons = buildTopPagingButtons(layer, parseInt(criset.current._page), !daton.isComplete);
            topContent = <>
                {topPagingButtons}
                <GridView session={session} rows={rt.table} datonDef={datonDef} tableDef={datonDef.mainTableDef} edit={isEditing} layer={layer} sortClicked={sortHandler} />
            </>;
        } else {
            topContent = null; //should not use grids with single main row
        }
    }

    //set criteriaContent to optional criteria card
    let criteriaContent: ReactElement|null = null;
    if (datonDef.criteriaDef) {
        criteriaContent = 
            <div className="criteria-block">
                <CardView session={session} criset={criset.current} datonDef={datonDef} tableDef={datonDef.criteriaDef} layer={layer} />
                <div>
                    <button className="search-button" onClick={doSearch}>Search</button>
                </div>
            </div>;
    }

    //set up banner props
    let bannerState = 0;
    if (isWorking) bannerState = -1;
    else if (isEditing) bannerState = 2;
    else if (datonDef.isPersiston && securityUtil.canEditPersiston(datonDef)) bannerState = 1;

    return (
        <div className="daton" ref={domElement}>
            <DatonBanner datonDef={datonDef} editState={bannerState} parsedDatonKey={parsedDatonKey!} editClicked={editClicked} saveClicked={saveClicked} 
                cancelClicked={cancelClicked} removeClicked={removeClicked} deleteClicked={deleteClicked} doExport={doExport} session={session}/>
            {errorItems.length > 0 && <ul className="daton-errors">
                {errorItems.map((s, idx3) => <li key={idx3}>{s}</li>)}
            </ul>}
            {criteriaContent}
            {topContent}
        </div>
    );
});