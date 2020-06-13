import {parseDatonKey, setRowValue} from 'retrodry';

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
    //  datonKey
    //  edit //true if edit mode 
    //  daton //noneditable daton version
    //  datonDef 
    //  lookupSelected //optional function when this layer is a viewon used for lookup; called when item selected; defined in startLookup()
    //}
    layers = [];

    //if set by host app, DatonView will call this after a successful save, passing args: (datonkey string)
    onLayerSaved;

    //incremented by DatonStack as a way to force rerender of datons (which use uncontrolled inputs)
    rerenderCount = 0;

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
            datonKey: key,
            edit: edit,
            daton: daton,
            datonDef: datonDef
        };
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
    async replaceKey(oldKey, newKey) {
        const idx = this.layers.findIndex(x => x.datonKey === oldKey);
        if (idx < 0) return;
        const daton = await this.session.get(newKey);
        if (!daton) return;
        const layer = this.layers[idx];
        layer.datonKey = newKey;
        layer.daton = daton;
        layer.edit = false;
        this.callOnChanged();
    }

    //called from click event on a foreign key in a grid
    async gridKeyClicked(ev, layer, tableDef, row, colDef) {
        ev.stopPropagation();

        //if this layer is a lookup for some other layer and the user clicked on the key of the main table,
        //then copy key/description back and close this layer
        if (layer.lookupSelected && layer.datonDef.mainTableDef === tableDef) {
            if (await layer.lookupSelected(row, colDef))
                return;
        }

        //fall through to here, so open the persiston referred to by the clicked key value
        const targetDatonKey = colDef.foreignKeyDatonTypeName + '|=' + row[colDef.name];
        this.add(targetDatonKey, false);
    }

    //called from click event on a lookup button to open a viewon for lookup
    async startLookup(editingLayer, editingTableDef, editingRow, editingColDef) {
        //add layer for viewon lookup
        const viewonDef = this.session.getDatonDef(editingColDef.lookupViewonTypeName);
        if (!viewonDef) return;
        const lookupLayer = await this.add(editingColDef.lookupViewonTypeName, false);

        //define callback when user clicks on a key in the viewon result row;
        //the function is called in gridKeyClicked and returns true on success
        lookupLayer.lookupSelected = async (viewonRow, clickedColDef) => {
            //abort if editing layer is no longer in the stack or is not in edit mode
            const editLayerIdx = this.layers.findIndex(x => x === editingLayer);
            if (editLayerIdx === -1 || !this.layers[editLayerIdx].edit) return false;

            //abort if clicked col is not the one we need for the editing col
            if (editingColDef.lookupViewonKeyColumnName !== clickedColDef.name) return false;

            //copy key value from viewon (this cascades to also update the description columns)
            await setRowValue(this.session, editingTableDef, editingColDef, editingRow, viewonRow[clickedColDef.name], null, null, viewonRow);
            //todo old: editingRow[editingColDef.name] = viewonRow[clickedColDef.name];
            // //set any leftjoin-defined columns in target row from viewon
            // for (let descrColDef of editingTableDef.cols) {
            //     if (descrColDef.leftJoinForeignKeyColumnName === editingColDef.name) {
            //         const descrValue = viewonRow[descrColDef.leftJoinRemoteDisplayColumnName];
            //         editingRow[descrColDef.name] = descrValue || null;
            //     }
            // }
            this.removeByKey(lookupLayer.datonKey);
            return true;
        };
    }

    callOnChanged() {if (this.onChanged) this.onChanged();}
}