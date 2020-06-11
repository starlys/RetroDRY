import {parseDatonKey} from 'retrodry';

//Nonvisual container for the state of a daton stack.
//To use, pass new instance as props to a DatonStack, then after the stack is rendered, any
//other code may call the other methods to manipulate the stack state.
export default class DatonStackState {
    //function to notify owner that the stack state changed
    onChanged;

    //retrodry session
    session;

    //each layer consists of {
    //  datonKey
    //  edit //true if edit mode
    //  daton //noneditable daton version
    //  datonDef 
    //}
    layers = [];

    //if set, DatonView will call this after a successful save, passing args: (daton)
    onLayerSaved;

    //called only by DatonStack in its initialization
    initialize(session, onChanged) { 
        this.session = session;
        this.onChanged = onChanged; 
    }

    //add a layer by daton key, optionally in initial edit mode
    async add(key, edit) {
        //if already there, exit
        const existingIdx = this.layers.findIndex(x => x.datonKey === key);
        if (existingIdx >= 0) return;

        //get daton or abort
        const parsedKey = parseDatonKey(key);
        const datonDef = this.session.getDatonDef(parsedKey.typeName);
        const daton = await this.session.get(key, {doSubscribeEdit: true});
        if (!daton) return;

        //add layer
        this.layers.push({
            datonKey: key,
            edit: edit,
            daton: daton,
            datonDef: datonDef
        });
        this.callOnChanged();
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

    //replace a viewon key, which performs a search on the new criteria; cannot be used to change the type!
    async replaceViewonKey(oldKey, newKey) {
        const idx = this.layers.findIndex(x => x.datonKey === oldKey);
        if (idx < 0) return;
        const daton = await this.session.get(newKey);
        if (!daton) return;
        const layer = this.layers[idx];
        layer.datonKey = newKey;
        layer.daton = daton;
        this.callOnChanged();
    }

    callOnChanged() {if (this.onChanged) this.onChanged();}
}