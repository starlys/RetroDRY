import { DataDictionaryResponse, GetDatonRequest, LongResponse, DatonDefResponse } from "./wireTypes";
import { Retrovert } from "./retrovert";
import DiffTool from "./diffTool";
import DatonKey from "./datonKey";
import SaveInfo from "./saveInfo";
import Utils from "./utils";
import GetOptions from "./getOptions";
import CloneTool from "./cloneTool";

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

    //if set, Session will call this whenever a daton was received because another session edited it
    onSubscriptionUpdate?: (daton: any) => void;

    private serverIdx: number = 0;

    //pristine persistons that are subscribed or locked, indexed by key
    private persistonCache: any = {};

    //pristine persiston version numbers that are in persistonCache, indexed by key
    private versionCache: any = {};

    //subscribe level (1=subscribed; 2=locked) indexed by persiston key
    private subscribeLevel: any = {};

    //start session; caller should check this.dataDictionary is truthy to see if it was successful
    async start(): Promise<void> {
        if (!this.serverList.length) throw new Error('server list not initialized');
        this.serverIdx = Math.floor(Math.random() * this.serverList.length); //random server
        await this.callInitialize();

        //start long polling
        setTimeout(() => this.longPoll(), 10000);
    }

    //get single daton from cache or server
    async get(datonKey: string, options?: GetOptions): Promise<any> {
        const responses = await this.getMulti([datonKey], options);
        return responses[0];
    }

    //get one or more datons from cache or server
    async getMulti(datonKeys: string[], options?: GetOptions): Promise<any[]> {
        this.ensureInitialized();
        if (!options) options = new GetOptions();

        //get from cache; array will have undefined elements for any keys that weren't found
        let datons: any[] = datonKeys.map(k => this.persistonCache[k]);

        //make list of requests for those we need get from server 
        const datonRequests: GetDatonRequest[] = [];
        for (let i = 0; i < datonKeys.length; ++i) {
            if (!datons[i] || options.forceCheckVersion) {
                const datonRequest = {
                    key: datonKeys[i],
                    doSubscribe: options.doSubscribeEdit,
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
            const response = await Utils.httpMain(this.baseServerUrl(), request);
            const isOk = response && !response.errorCode;
            if (!isOk) throw new Error('Get failed: ' + response.errorCode);

            //reinflate what we got back; if any daton is missing from the response, it means the known version was the most up to date
            if (response.condensedDatons) {
                for (let condensed of response.condensedDatons) {
                    const daton = Retrovert.expandCondensedDaton(this.dataDictionary!, condensed);

                    //stick this one in the datons list in the right place, as defined by the position of the daton key in the caller's array
                    const idxOfKey = datonKeys.findIndex(d => d === daton.key);
                    if (idxOfKey < 0) throw new Error('Server returned daton key that was not requested');
                    datons[idxOfKey] = daton;

                    if (this.onReceiveDaton) this.onReceiveDaton(daton);
                }
            }
        }

        //check if anything got lost; if so, there is a code bug
        if (datons.some(d => !d)) throw new Error('Daton missing'); 

        //cache if subscribing
        if (options.doSubscribeEdit) {
            for (let i = 0; i < datons.length; ++i) {
                let daton = datons[i];
                this.persistonCache[daton.key] = daton;
                const oldSubscribeLevel = this.subscribeLevel[daton.key];
                if (!oldSubscribeLevel)
                    this.subscribeLevel[daton.key] = 1;
                this.versionCache[daton.key] = daton.version;
    
                //clone it so caller cannot clobber our nice cached version
                const parsedKey = DatonKey.parse(daton.key);
                const datondef = Utils.getDatonDef(this.dataDictionary!, parsedKey.typeName);
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
        const response = await Utils.httpMain(this.baseServerUrl(), request);

        if (!response.manageDatons) return {};
        const ret: any = {};
        for (const responseDetail of response.manageDatons) {
            ret[responseDetail.key] = responseDetail.errorCode;
            if (responseDetail.subscribeState === 0) {
                delete this.persistonCache[responseDetail.key];
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
            const datonkey  = DatonKey.parse(modified.key);
            const pristine = this.persistonCache[modified.key];
            const subscribeLevel = this.subscribeLevel[modified.key];
            if (!datonkey.isNew()) {
                if (!pristine || !subscribeLevel) throw new Error('daton cannot be saved because it was not cached; you must get with doSubscribeEdit:true before making edits');
            }
            
            //determine if lock needed
            if (pristine && subscribeLevel === 1) datonsToLock.push(pristine);

            //generate diff
            const datondef = Utils.getDatonDef(this.dataDictionary!, datonkey.typeName);
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
        const saveResponse = await Utils.httpMain(this.baseServerUrl(), request);

        //unlock whatever we locked (even if failed save)
        if (datonsToLock.length) {
            await this.changeSubscribeState(datonsToLock, 1);
        }

        //host app callback
        if (this.onSave && saveResponse.savePersistonsSuccess) {
            for (let daton in datons) this.onSave(daton);
        }

        //error reporting
        const ret = { success: saveResponse?.savePersistonsSuccess || false, details: saveResponse?.savedPersistons || []};
        return ret;
    }

    //get the daton definition by type name (camelCase name)
    getDatonDef(name: string): DatonDefResponse|undefined {
        return this.dataDictionary?.datonDefs.find(d => d.name === name);
    }

    //end retroDRY session; stop long polling
    async quit() {
        //free up memory and reset so that it could be initialized again
        this.persistonCache = {};
        this.versionCache = {};
        this.subscribeLevel = {};
        this.dataDictionary = undefined;

        const request = { sessionKey: this.sessionKey, doQuit: true };
        await Utils.httpMain(this.baseServerUrl(), request);
    }

    private async callInitialize() {
        const request = { sessionKey: this.sessionKey, initialze: { languageCode: this.languageCode}};
        const response = await Utils.httpMain(this.baseServerUrl(), request);
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

        try {
            const response = await Utils.httpPost<LongResponse>(this.baseServerUrl() + 'retro/long', { sessionKey: this.sessionKey });
            if (response?.errorCode) {
                console.log(response.errorCode);
                return; //ends long polling permanently
            }
            if (response?.permissionSet) {
                //todo store permissions
            }
            if (response?.condensedDatons) {
                for (let condensed of response.condensedDatons) {
                    const daton = Retrovert.expandCondensedDaton(this.dataDictionary!, condensed);
                    this.persistonCache[daton.key] = daton;
                    this.versionCache[daton.key] = daton.version;
                    if (this.onSubscriptionUpdate) this.onSubscriptionUpdate(daton);
                }
            }
        }
        catch (e) {
            console.log(e);
        }

        //restart polling indefinitely
        setTimeout(() => this.longPoll(), this.longPollDelay); 
    }
}