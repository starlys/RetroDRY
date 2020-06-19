import {parseDatonKey, afterSetRowValue} from 'retrodry';

//Nonvisual container for the state of a daton stack.
//To use, pass new instance as props to a DatonStack, then after the stack is rendered, any
//other code may call the other methods to manipulate the stack state.
export default class DatonStackState {
    //function to notify DatonStack owner that the stack state changed
    onChanged;

    //retrodry session
    session;

    //each layer consists of {
    //  stackstate //reference to owning object
    //  renderCount //integer, incremented to cause the DatonView to be rerendered
    //  mountCount //integer, incremented to cause the DatonView to be remounted
    //  datonKey
    //  parsedDatonKey
    //  edit //true if edit mode 
    //  daton //noneditable daton version
    //  datonDef 
    //  lookupSelected //optional function when this layer is a viewon used for lookup; called when item selected; defined in startLookup()
    //  propagateSaveToViewon //optional function when this layer is a persiston that was opened from a viewon; reflects persison changes in the displayed viewon; defined in gridKeyClicked
    //}
    layers = [];

    //if set by host app, DatonView will call this after a successful save, passing args: (datonkey string)
    onLayerSaved;

    //called only by DatonStack in its initialization
    initialize(session, onChanged) { 
        this.session = session;
        this.onChanged = onChanged; 
    }

    //add a layer by daton key, optionally in initial edit mode; return layer
    async add(key, edit) {
        //if already there, exit
        const existingIdx = this.layers.findIndex(x => x.datonKey === key);
        if (existingIdx >= 0) return this.layers[existingIdx];

        //get daton or abort
        const parsedKey = parseDatonKey(key);
        const datonDef = this.session.getDatonDef(parsedKey.typeName);
        const daton = await this.session.get(key, {doSubscribeEdit: true});
        if (!daton) return;

        //add layer
        const layer = {
            stackstate: this,
            renderCount: 0,
            mountCount: 0,
            datonKey: key,
            parsedDatonKey: parsedKey,
            edit: edit,
            daton: daton,
            datonDef: datonDef
        };
        this.layers.push(layer);
        this.callOnChanged();
        return layer;
    }

    //add a layer by viewon type name (having no initial rows); return layer
    addEmptyViewon(datonType) {
        //if already there, exit
        const existingIdx = this.layers.findIndex(x => x.datonKey === datonType);
        if (existingIdx >= 0) return this.layers[existingIdx];
        const datonDef = this.session.getDatonDef(datonType);

        //add layer
        const layer = {
            stackstate: this,
            renderCount: 0,
            mountCount: 0,
            datonKey: datonType,
            edit: false,
            daton: this.session.createEmptyViewon(datonType),
            datonDef: datonDef
        };
        layer.parsedDatonKey = parseDatonKey(layer.daton.key);
        this.layers.push(layer);
        this.callOnChanged();
        return layer;
    }

    //remove a layer by its daton key and optionally unsubscribe
    removeByKey(key, doUnsubscribe) {
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
    //OR replace a new persiston key with the actual key; cannot be used to change the type!
    async replaceKey(oldKey, newKey, forceLoad) {
        const idx = this.layers.findIndex(x => x.datonKey === oldKey);
        if (idx < 0) return;
        const daton = await this.session.get(newKey, {forceCheckVersion: forceLoad});
        if (!daton) return;
        const layer = this.layers[idx];
        layer.datonKey = newKey;
        layer.daton = daton;
        layer.edit = false;
        ++layer.renderCount;
        ++layer.mountCount;
        this.callOnChanged();
    }

    //called from click event on a foreign key in a grid
    async gridKeyClicked(ev, gridLayer, gridTableDef, gridRow, gridColDef) {
        ev.stopPropagation();

        //if this layer is a lookup for some other layer and the user clicked on the key of the main table,
        //then copy key/description back and close this layer
        if (gridLayer.lookupSelected && gridLayer.datonDef.mainTableDef === gridTableDef) {
            if (await gridLayer.lookupSelected(gridRow, gridColDef))
                return;
        }

        //fall through to here, so open the persiston referred to by the clicked key value
        const targetDatonKey = gridColDef.foreignKeyDatonTypeName + '|=' + gridRow[gridColDef.name];
        const editLayer = await this.add(targetDatonKey, false);

        //set up behavior for changes saved on edit layer to show up in the calling viewon
        editLayer.propogateSaveToViewon = (persiston) => {
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
            ++gridLayer.renderCount;
            this.callOnChanged();
        };
    }

    //called from click event on a lookup button to open a viewon for lookup
    async startLookup(editingLayer, editingTableDef, editingRow, editingColDef) {
        //add layer for viewon lookup
        const viewonDef = this.session.getDatonDef(editingColDef.lookupViewonTypeName);
        if (!viewonDef) return;
        const lookupLayer = await this.addEmptyViewon(editingColDef.lookupViewonTypeName);

        //define callback when user clicks on a key in the viewon result row;
        //the function is called in gridKeyClicked and returns true on success
        lookupLayer.lookupSelected = async (viewonRow, clickedColDef) => {
            //abort if editing layer is no longer in the stack or is not in edit mode
            const editLayerIdx = this.layers.findIndex(x => x === editingLayer);
            if (editLayerIdx === -1) return false;
            const editingPersiston = editingLayer.parsedDatonKey.isPersiston();
            if (editingPersiston && !editingLayer.edit) return false;

            //abort if clicked col is not the one we need for the editing col
            if (editingColDef.lookupViewonKeyColumnName !== clickedColDef.name) return false;

            //copy key value from viewon then cascade to also update the description columns
            let fkValue = viewonRow[clickedColDef.name];
            if (!editingPersiston) fkValue = fkValue.toString(); //viewon criteria must be strings
            editingRow[editingColDef.name] = fkValue;
            ++editingLayer.renderCount;
            this.callOnChanged();
            await afterSetRowValue(this.session, editingTableDef, editingColDef, editingRow, null, null, viewonRow);
            this.removeByKey(lookupLayer.datonKey);
            ++editingLayer.renderCount;
            return true;
        };
    }

    callOnChanged() {if (this.onChanged) this.onChanged();}
}