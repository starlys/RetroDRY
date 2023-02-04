import {parseDatonKey, afterSetRowValue, DatonKey, DatonDefResponse, ColDefResponse, Session, TableDefResponse} from 'retrodryclient';

export interface DatonStackLayer {
    stackstate: DatonStackState; //reference to owning object
    rerender: () => void; // convenience function to increment renderCount and call changed function
    renderCount: number; //integer, incremented to cause the DatonView to be rerendered
    mountCount: number; //integer, incremented to cause the DatonView to be remounted
    datonKey: string;
    parsedDatonKey: DatonKey;
    edit: boolean; //true if edit mode 
    daton: any; //noneditable daton version
    datonDef: DatonDefResponse;
    businessContext: string; // '' for default or an app-defined context; affects layout selection
    lookupSelected?: (row: any, colDef: ColDefResponse) => Promise<boolean>; //optional function when this layer is a viewon used for lookup; called when item selected; defined in startLookup()
    propagateSaveToViewon?: (viewon: any) => void; //optional function when this layer is a persiston that was opened from a viewon; reflects persison changes in the displayed viewon; defined in gridKeyClicked

}

//Nonvisual container for the state of a daton stack.
//To use, pass new instance as props to a DatonStack, then after the stack is rendered, any
//other code may call the other methods to manipulate the stack state.
export default class DatonStackState {
    //function to notify DatonStack owner that the stack state changed
    onChanged?: () => void;

    //retrodry session
    session?: Session;

    layers: DatonStackLayer[] = [];

    //if set by host app, DatonView will call this after a successful save, passing args: (datonkey string)
    onLayerSaved?: (datonKey:string) => void;

    //if set by host app, the lookup button will call this function; the function should return the viewon key to use for lookup
    //or null to prevent lookup, or false to revert to noncustom behavior. 
    //The injected function gets passed parameters 
    onCustomLookup?: (editingLayer: DatonStackLayer, editingTableDef: TableDefResponse, editingRow: any, editingColDef: ColDefResponse) => string|boolean|null;

    //called only by DatonStack in its initialization
    initialize(session: Session, onChanged: () => void) { 
        this.session = session;
        this.onChanged = onChanged; 
    }

    //create layer object; caller should add this to layers array
    createLayer(datonDef: DatonDefResponse, datonKey: string, parsedDatonKey: DatonKey, daton: any): DatonStackLayer {
        return {
            stackstate: this,
            renderCount: 0,
            mountCount: 0,
            edit: false,
            datonKey: datonKey,
            parsedDatonKey: parsedDatonKey,
            daton: daton,
            datonDef: datonDef,
            businessContext: '',
            rerender: function() { ++this.renderCount; this.stackstate.callOnChanged(); }
        };
    }

    //add a layer by daton key, optionally in initial edit mode; return layer;
    //businessContext will be set to default ('') if missing
    async add(key: string, edit: boolean, businessContext?: string) {
        if (!this.session) return;

        //if already there, exit
        const existingIdx = this.layers.findIndex(x => x.datonKey === key);
        if (existingIdx >= 0) return this.layers[existingIdx];

        //get daton or abort
        const parsedKey = parseDatonKey(key);
        const datonDef = this.session.getDatonDef(parsedKey.typeName);
        const daton = await this.session.get(key, {isForEdit: parsedKey.isPersiston()});
        if (!daton || !datonDef) return;

        //add layer
        const layer = this.createLayer(datonDef, key, parsedKey, daton);
        layer.edit = edit;
        layer.businessContext = businessContext || '';
        this.layers.push(layer);
        this.callOnChanged();
        return layer;
    }

    //add a layer by viewon type name (having no initial rows); return layer
    //businessContext will be set to default ('') if missing
    async addEmptyViewon(datonType: string, businessContext?: string): Promise<any> {
        if (!this.session) return;

        //if already there, exit
        const existingIdx = this.layers.findIndex(x => x.datonKey === datonType);
        if (existingIdx >= 0) return this.layers[existingIdx];
        const datonDef = this.session.getDatonDef(datonType);
        if (!datonDef) return;

        //add layer
        const viewon = this.session.createEmptyViewon(datonType);
        const layer = this.createLayer(datonDef, datonType, parseDatonKey(viewon.key), viewon);
        layer.businessContext = businessContext || '';
        this.layers.push(layer);
        this.callOnChanged();
        return layer;
    }

    //remove a layer by its daton key and optionally unsubscribe
    removeByKey(key: string, doUnsubscribe: boolean) {
        if (!this.session) return;

        const idx = this.layers.findIndex(x => x.datonKey === key);
        if (idx >= 0) {
            if (doUnsubscribe) {
                this.session.changeSubscribeState([this.layers[idx].daton], 0);
            }
            this.layers.splice(idx, 1);
            this.callOnChanged();
        }
    }

    //replace a viewon key, which performs a search on the new criteria;
    //OR replace a new persiston key with the actual key
    async replaceKey(oldKey: string, newKey: string, forceLoad: boolean) {
        if (!this.session) return;

        const daton = await this.session.get(newKey, {forceCheckVersion: forceLoad});
        this.replaceDaton(oldKey, daton);
    }

    //replace a daton in the stack with a different, already-loaded daton
    replaceDaton(oldKey: string, daton: any) {
        if (!daton) return;
        const idx = this.layers.findIndex(x => x.datonKey === oldKey);
        if (idx < 0) return;
        const layer = this.layers[idx];
        layer.datonKey = daton.key;
        layer.daton = daton;
        layer.edit = false;
        ++layer.renderCount;
        ++layer.mountCount;
        this.callOnChanged();
    }

    //called when column header clicked on main viewon table
    async doSort(layer: DatonStackLayer, colName: string) {
        //if we can sort in memory, do that
        if (layer.daton.isComplete) {
            const rows = layer.daton[layer.datonDef.mainTableDef.name];
            rows.sort((a:any, b:any) => a[colName] === b[colName] ? 0 : (a[colName] < b[colName] ? -1 : +1));
            layer.rerender();
        }

        //incompletely loaded viewons must sort on server
        else {
            const keyWithSort = parseDatonKey(layer.datonKey);
            const pageSegIdx = keyWithSort.otherSegments.findIndex(s => s.indexOf('_page=') === 0);
            if (pageSegIdx >= 0) keyWithSort.otherSegments.splice(pageSegIdx, 1);
                const sortSegIdx = keyWithSort.otherSegments.findIndex(s => s.indexOf('_sort=') === 0);
            if (sortSegIdx >= 0) keyWithSort.otherSegments.splice(sortSegIdx, 1);
            keyWithSort.otherSegments.push('_sort=' + colName);
            await this.replaceKey(layer.datonKey, keyWithSort.toKeyString(), false);
        }
    }

    //called when a page button clicked on main viewon table
    async goToPage(layer: DatonStackLayer, pageNo: number) {
        const keyWithPage = parseDatonKey(layer.datonKey);
        const pageSegIdx = keyWithPage.otherSegments.findIndex(s => s.indexOf('_page=') === 0);
        if (pageSegIdx >= 0) keyWithPage.otherSegments.splice(pageSegIdx, 1);
        if (pageNo) keyWithPage.otherSegments.push('_page=' + pageNo);
        await this.replaceKey(layer.datonKey, keyWithPage.toKeyString(), false);
    }

    //called from click event on a foreign key in a grid
    async gridKeyClicked(ev: any, gridLayer: DatonStackLayer, gridTableDef: TableDefResponse, gridRow: any, gridColDef: ColDefResponse) {
        ev.stopPropagation();

        //if this layer is a lookup for some other layer and the user clicked on the key of the main table,
        //then copy key/description back and close this layer
        if (gridLayer.lookupSelected && gridLayer.datonDef.mainTableDef === gridTableDef) {
            if (await gridLayer.lookupSelected(gridRow, gridColDef))
                return;
        }

        //fall through to here, so open the persiston referred to by the clicked key value
        const targetDatonKey = gridColDef.foreignKeyDatonTypeName + '|=' + gridRow[gridColDef.name];
        const editLayer = await this.add(targetDatonKey, false, gridLayer.businessContext);
        if (!editLayer) return;

        //set up behavior for changes saved on edit layer to show up in the calling viewon
        editLayer.propagateSaveToViewon = (persiston: any) => {
            //abort if viewon layer is no longer in the stack 
            const gridLayerIdx = this.layers.findIndex(x => x === gridLayer);
            if (gridLayerIdx === -1) return;

            //abort if grid isn't part of a viewon or persiston isn't single-main-row
            if (parseDatonKey(gridLayer.datonKey).isPersiston()) return;
            if (editLayer.datonDef.multipleMainRows) return;

            //copy anything over where column names match
            for (let targetColDef of gridTableDef.cols) {
                const sourceColDef = editLayer.datonDef.mainTableDef.cols.find(c => c.name === targetColDef.name);
                if (sourceColDef) gridRow[targetColDef.name] = persiston[sourceColDef.name];
            }
            gridLayer.rerender();
        };
    }

    //called from click event on a lookup button to open a viewon for lookup
    async startLookup(editingLayer: DatonStackLayer, editingTableDef: TableDefResponse, editingRow: any, editingColDef: ColDefResponse) {
        if (!this.session) return;

        //add layer for viewon lookup using custom behavior
        let lookupLayer: DatonStackLayer|undefined;
        let useStandard = true;
        if (this.onCustomLookup) {
            const key = this.onCustomLookup(editingLayer, editingTableDef, editingRow, editingColDef);
            useStandard = key === false;
            if (!useStandard) {
                if (!key) return; //custom behavior is explicitly canceled
                lookupLayer = await this.add(key as string, false, editingLayer.businessContext);
            }
        } 
        if (useStandard) {
            //or standard behavior
            const viewonDef = this.session.getDatonDef(editingColDef.selectBehavior.viewonTypeName);
            if (!viewonDef) return;
            lookupLayer = await this.addEmptyViewon(editingColDef.selectBehavior.viewonTypeName, editingLayer.businessContext);
        }
        if (!lookupLayer) return;

        //define callback when user clicks on a key in the viewon result row;
        //the function is called in gridKeyClicked and returns true on success
        lookupLayer.lookupSelected = async (viewonRow: any, clickedColDef: ColDefResponse): Promise<boolean> => {
            if (!this.session || !lookupLayer) return false;

            //abort if editing layer is no longer in the stack or is not in edit mode
            const editLayerIdx = this.layers.findIndex(x => x === editingLayer);
            if (editLayerIdx === -1) return false;
            const editingPersiston = editingLayer.parsedDatonKey.isPersiston();
            if (editingPersiston && !editingLayer.edit) return false;

            //abort if clicked col is not the one we need for the editing col
            if (editingColDef.selectBehavior.viewonValueColumnName !== clickedColDef.name) return false;

            //copy key value from viewon then cascade to also update the description columns
            let fkValue = viewonRow[clickedColDef.name];
            if (!editingPersiston) fkValue = fkValue.toString(); //viewon criteria must be strings
            editingRow[editingColDef.name] = fkValue;
            ++editingLayer.renderCount;
            this.callOnChanged();
            await afterSetRowValue(this.session, editingTableDef, editingColDef, editingRow, '', null, viewonRow);
            this.removeByKey(lookupLayer.datonKey, false);
            ++editingLayer.renderCount;
            return true;
        };
    }

    callOnChanged() {if (this.onChanged) this.onChanged();}
}