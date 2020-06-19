import { DataDictionaryResponse, GetDatonRequest, LongResponse, DatonDefResponse } from "./wireTypes";
import { Retrovert } from "./retrovert";
import DiffTool from "./diffTool";
import {parseDatonKey} from "./datonKey";
import SaveInfo from "./saveInfo";
import NetUtils from "./netUtils";
import GetOptions from "./getOptions";
import CloneTool from "./cloneTool";
import { PanelLayout, GridLayout } from "./layout";
import { RecurPoint } from "./recurPoint";
import Mutex from "./mutex";

export default class Session {
    //delay in millis between end of long polling response and sending next long poll request
    //(can be lengthened for integration testing so we don't hit the browser's limit of open connections to the server)
    longPollDelay: number = 50;

    //UTC + this number of minutes = local time; caller should set this
    timeZoneOffset: number = 0;
   
    //server URLs to which this class will append 'retro/...'; caller should set this
    serverList: string[] = [];

    //server assigned key that must be fetched outside of RetroDRY and set here;
    //the code that sets this value should have already authenticated the user
    sessionKey: string = '';

    //empty string for the default language or a code supported by the server; caller should set this
    languageCode: string = '';

    //the data dictionary; set in start()
    dataDictionary?: DataDictionaryResponse;

    //if set, Session will call this whenever a daton is received from the server (when initiated by this client)
    onReceiveDaton?: (daton: any) => void;

    //if set, Session will call this whenever a daton is saved successfully, passing the modified local version
    onSave?: (daton: any) => void;

    //if set, Session will call this whenever a daton was received from the server because this session or another session edited it
    onSubscriptionUpdate?: (daton: any) => void;

    private serverIdx: number = 0;

    //pristine persistons that are subscribed or locked, indexed by key
    private persistonCache: any = {};

    //pristine persiston version numbers that are in persistonCache, indexed by key
    private versionCache: any = {};

    //subscribe level (1=subscribed; 2=locked) indexed by persiston key
    private subscribeLevel: any = {};

    //cardLayouts and gridLayouts are indexed by "datonName|tableName"
    private cardLayouts: {[name: string]: PanelLayout} = {};
    private gridLayouts: {[name: string]: GridLayout} = {};

    //ensures only one server get call is in progress (this ensures that multiple calls for the same daton don't go to the server twice)
    private getDatonMutex: Mutex = new Mutex();

    //start session; caller should check this.dataDictionary is truthy to see if it was successful
    async start(): Promise<void> {
        if (!this.serverList.length) throw new Error('server list not initialized');
        this.serverIdx = Math.floor(Math.random() * this.serverList.length); //random server
        await this.callInitialize();

        //start long polling
        setTimeout(() => this.longPoll(), 10000);
    }

    //register default card layout to use with the given daton and table
    registerCardLayout(datonName: string, tableName: string, layout: PanelLayout) {
        this.cardLayouts[datonName + '|' + tableName] = layout;
    }

    //register default grid layout to use with the given daton and table
    registerGridLayout(datonName: string, tableName: string, layout: GridLayout) {
        this.gridLayouts[datonName + '|' + tableName] = layout;
    }

    //get the registered or autogenerated card layout for the given daton type and table
    getCardLayout(datonName: string, tableName: string): PanelLayout {
        const key = datonName + '|' + tableName;
        let layout = this.cardLayouts[key];
        if (!layout) {
            const tableDef = this.getTableDef(datonName, tableName);
            if (tableDef) layout = PanelLayout.autoGenerate(tableDef);
            this.cardLayouts[key] = layout;
        }
        return layout;
    }

    //get the registered or autogenerated grid layout for the given daton type and table
    getGridLayout(datonName: string, tableName: string): GridLayout {
        const key = datonName + '|' + tableName;
        let layout = this.gridLayouts[key];
        if (!layout) {
            const tableDef = this.getTableDef(datonName, tableName);
            if (tableDef) layout = GridLayout.autoGenerate(tableDef);
            this.gridLayouts[key] = layout;
        }
        return layout;
    }

    //get single daton from cache or server; or undefined if not found
    async get(datonKey: string, options?: GetOptions): Promise<any> {
        const responses = await this.getMulti([datonKey], options);
        return responses[0];
    }

    //create a valid empty viewon locally (use this for seeding a searchable viewon for display without loading all rows)
    createEmptyViewon(datonType: string): any {
        return {
            key: datonType
        };
    }

    //get one or more datons from cache or server; if any requested datons are not found, they will be omitted from the results
    //so it will not always return an array of the same size as the provided keys array
    async getMulti(datonKeys: string[], options?: GetOptions): Promise<any[]> {
        this.ensureInitialized();
        if (!options) options = new GetOptions();
        await this.getDatonMutex.acquire();
        try {
            return await this.getMultiSingleThread(datonKeys, options);
        } finally {
            this.getDatonMutex.release();
        }
    }

    //see getMulti
    private async getMultiSingleThread(datonKeys: string[], options: GetOptions): Promise<any[]> {
        //convert datonKeys to parallel array of parsed keys
        const parsedDatonKeys = datonKeys.map(k => parseDatonKey(k));

        //get from cache; array will have undefined elements for any keys that weren't found
        let datons: any[] = datonKeys.map(k => this.persistonCache[k]);

        //make list of requests for those we need get from server 
        const datonRequests: GetDatonRequest[] = [];
        for (let i = 0; i < datonKeys.length; ++i) {
            if (!datons[i] || options.forceCheckVersion) {
                const isNew = parsedDatonKeys[i].isNew();
                const datonRequest = {
                    key: datonKeys[i],
                    doSubscribe: options.doSubscribeEdit && !isNew,
                    forceLoad: options.forceCheckVersion,
                    knownVersion: this.versionCache[datonKeys[i]]
                };
                datonRequests.push(datonRequest);
            }
        }

        //get from server
        if (datonRequests.length) {
            const request = { 
                sessionKey: this.sessionKey, 
                getDatons: datonRequests
            };
            const response = await NetUtils.httpMain(this.baseServerUrl(), request);
            const isOk = response && !response.errorCode;
            if (!isOk) throw new Error('Get failed: ' + response.errorCode);

            //reinflate what we got back; if any daton is missing from the response, it means the known version was the most up to date
            if (response.condensedDatons) {
                for (let condensed of response.condensedDatons) {
                    const daton = Retrovert.expandCondensedDaton(this, condensed);

                    //stick this one in the datons list in the right place, as defined by the position of the daton key in the caller's array
                    const idxOfKey = datonKeys.findIndex(d => d === daton.key);
                    if (idxOfKey < 0) throw new Error('Server returned daton key that was not requested');
                    datons[idxOfKey] = daton;

                    if (this.onReceiveDaton) this.onReceiveDaton(daton);
                }
            }
        }

        //eliminate empty slots in the return array (for datons that could not be loaded)
        datons = datons.filter(d => !!d);

        //cache and clone if subscribing (unless is new)
        if (options.doSubscribeEdit) {
            for (let i = 0; i < datons.length; ++i) {
                let daton = datons[i];
                const parsedKey = parseDatonKey(daton.key);
                if (parsedKey.isNew()) continue;
                if (!parsedKey.isPersiston()) continue;

                //cache
                this.persistonCache[daton.key] = daton;
                const oldSubscribeLevel = this.subscribeLevel[daton.key];
                if (!oldSubscribeLevel)
                    this.subscribeLevel[daton.key] = 1;
                this.versionCache[daton.key] = daton.version;
    
                //clone it so caller cannot clobber our nice cached version
                const datondef = this.getDatonDef(parsedKey.typeName);
                if (!datondef) throw new Error('Unknown type name ' + parsedKey.typeName);
                daton = CloneTool.clone(datondef, daton);
                datons[i] = daton;
            }
        }
        
        return datons;
    }

    //get subscription state by daton key (see changeSubscriptionState for meaning of values)
    getSubscribeState(datonkey: string): 0|1|2 {
        return this.subscribeLevel[datonkey] || 0;
    }

    //change the subscription/lock state of one or more previously-cached persistons
    //to 0 (forgotten), 1 (cached and subscribed), or 2 (cached and locked).
    //returns object with error codes indexed by daton key
    async changeSubscribeState(datons: any[], subscribeState:0|1|2): Promise<any> {
        const requestDetails: any = [];
        for (const daton of datons) {
            if (!daton.key) throw new Error('Daton key missing');
            const currentState = this.subscribeLevel[daton.key] || 0;
            if (currentState === 0 && subscribeState !== 0)
                throw new Error('Can only change subscribe state if initial get was subscribed');
            if (subscribeState !== currentState)
                requestDetails.push({ 
                    key: daton.key, 
                    subscribeState: subscribeState,
                    version: this.versionCache[daton.key]
                });
        }
        if (!requestDetails.length) return {};
        const request = {
            sessionKey: this.sessionKey,
            manageDatons: requestDetails
        };
        const response = await NetUtils.httpMain(this.baseServerUrl(), request);

        if (!response.manageDatons) return {};
        const ret: any = {};
        for (const responseDetail of response.manageDatons) {
            ret[responseDetail.key] = responseDetail.errorCode;
            if (responseDetail.subscribeState === 0) {
                delete this.persistonCache[responseDetail.key];
                delete this.versionCache[responseDetail.key];
                delete this.subscribeLevel[responseDetail.key];
            } else {
                this.subscribeLevel[responseDetail.key] = responseDetail.subscribeState;
            }
        }
        return ret;
    }

    //save any number of datons in one transaction; the objects passed in should be abandoned by the caller after a successful save;
    //returns savePersistonResponse for each daton, and an overall success flag
    async save(datons: any[]): Promise<SaveInfo> {
        this.ensureInitialized();
        const diffs: any[] = [];
        const datonsToLock: any[] = [];
        for(let idx = 0; idx < datons.length; ++idx) {
            const modified = datons[idx];
            if (!modified.key) throw new Error('daton.key required');

            //get pristine version
            const datonkey = parseDatonKey(modified.key);
            let pristine = null;
            if (!datonkey.isNew())
                pristine = this.persistonCache[modified.key];
            const subscribeLevel = this.subscribeLevel[modified.key];
            if (!datonkey.isNew()) {
                if (!pristine || !subscribeLevel) throw new Error('daton cannot be saved because it was not cached; you must get with doSubscribeEdit:true before making edits');
            }
            
            //determine if lock needed
            if (pristine && subscribeLevel === 1) datonsToLock.push(pristine);

            //generate diff
            const datondef = this.getDatonDef(datonkey.typeName);
            if (!datondef) throw new Error('invalid type name');
            const diff = DiffTool.generate(datondef, pristine, modified); //pristine is falsy for new single-main-row persistons here
            if (diff)
                diffs.push(diff);
        }

        //if any need to be locked, do that before save, and recheck all are actually locked
        if (datonsToLock.length) {
            await this.changeSubscribeState(datonsToLock, 2);
            const anyunlocked = datonsToLock.some(d => this.subscribeLevel[d.key] !== 2);
            if (anyunlocked) throw new Error('Could not get lock');
        }

        //save on server
        const request = {
            sessionKey: this.sessionKey,
            saveDatons: diffs
        };
        const saveResponse = await NetUtils.httpMain(this.baseServerUrl(), request);

        //unlock whatever we locked (even if failed save)
        if (datonsToLock.length) {
            await this.changeSubscribeState(datonsToLock, 1);
        }

        //manage cache changes: forget version numbers but remember the saved version in case it gets displayed immediately after editing
        //(But note this newly cached saved version can't be edited again; we have to refetch from database for that)
        if (saveResponse?.savePersistonsSuccess) {
            for (let daton of datons) {
                const datonkey = parseDatonKey(daton.key);
                delete this.versionCache[daton.key];
                if (!datonkey.isNew())
                    this.persistonCache[daton.key] = daton;
            }
        }

        //host app callback
        if (this.onSave && saveResponse?.savePersistonsSuccess) {
            for (let daton of datons) this.onSave(daton);
        }

        //error reporting
        const ret = { success: saveResponse?.savePersistonsSuccess || false, details: saveResponse?.savedPersistons || []};
        return ret;
    }

    //get the daton definition by type name (camelCase name)
    getDatonDef(name: string): DatonDefResponse|undefined {
        return this.dataDictionary?.datonDefs.find(d => d.name === name);
    }

    //get the table definition by daton type name and table name (camelCase names)
    getTableDef(datonName: string, tableName: string) {
        const datonDef = this.getDatonDef(datonName);
        if (!datonDef) return null;
        if (datonDef.criteriaDef && datonDef.criteriaDef.name === tableName)
            return datonDef.criteriaDef;
        const tables = RecurPoint.getTables(datonDef);
        return tables.find(t => t.name === tableName);
    }

    //end retroDRY session; stop long polling
    async quit() {
        //free up memory and reset so that it could be initialized again
        this.persistonCache = {};
        this.versionCache = {};
        this.subscribeLevel = {};
        this.dataDictionary = undefined;

        const request = { sessionKey: this.sessionKey, doQuit: true };
        await NetUtils.httpMain(this.baseServerUrl(), request);
    }

    private async callInitialize() {
        const request = { sessionKey: this.sessionKey, initialze: { languageCode: this.languageCode}};
        const response = await NetUtils.httpMain(this.baseServerUrl(), request);
        if (response?.dataDictionary)
            this.dataDictionary = response.dataDictionary;
        if (response?.permissionSet) {
            //todo store permissions
        }
    }
    
    private baseServerUrl(): string {
        return this.serverList[this.serverIdx];
    }

    private ensureInitialized() {
        if (!this.dataDictionary) throw new Error('Session not initialized'); 
    }

    private async longPoll(): Promise<any> {
        if (!this.dataDictionary) return; //ends long polling permanently

        let pollOk = false;
        try {
            const response = await NetUtils.httpPost<LongResponse>(this.baseServerUrl() + 'retro/long', { sessionKey: this.sessionKey });
            if (response?.errorCode) {
                console.log(response.errorCode);
                return; //ends long polling permanently
            }
            pollOk = true;
            if (response?.permissionSet) {
                //todo store permissions
            }
            if (response?.condensedDatons) {
                for (let condensed of response.condensedDatons) {
                    const daton = Retrovert.expandCondensedDaton(this, condensed);
                    this.persistonCache[daton.key] = daton;
                    this.versionCache[daton.key] = daton.version;
                    if (this.onSubscriptionUpdate) this.onSubscriptionUpdate(daton);
                }
            }
        }
        catch (e) {
            //404 and other http errors can end up here
            console.log(e);
        }

        //restart polling indefinitely
        const delay = this.longPollDelay + (pollOk ? 0 : 20000);
        setTimeout(() => this.longPoll(), delay); 
    }
}