import { DataDictionaryResponse } from "./wireTypes";
import { Retrovert } from "./retrovert";
import DiffTool from "./diffTool";
import DatonKey from "./datonKey";
import SaveInfo from "./saveInfo";
import Utils from "./utils";
import GetOptions from "./getOptions";
import CloneTool from "./cloneTool";

export default class Session {
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

    //private timer: any;
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
        //todo
    }

    //get single daton from cache or server
    async get(datonKey: string, options?: GetOptions): Promise<any> {
        this.ensureInitialized();
        if (!options) options = new GetOptions();

        //get from cache
        let daton: any = this.persistonCache[datonKey];
        if (daton && !options.forceCheckVersion) return daton;

        //get from server
        const datonRequest = {
            key: datonKey,
            doSubscribe: options.doSubscribe,
            forceLoad: options.forceCheckVersion,
            knownVersion: this.versionCache[datonKey]
        };
        const request = { 
            sessionKey: this.sessionKey, 
            getDatons: [datonRequest]
        };
        const response = await Utils.httpMain(this.baseServerUrl(), request);
        const isOk = response && !response.errorCode;
        if (!isOk) throw new Error('Get failed: ' + response.errorCode);

        //either the known version was up to date or the server supplied it
        if (response.condensedDatons && response.condensedDatons.length == 1) {
            const condensedDaton = response?.condensedDatons?.[0];
            daton = Retrovert.expandCondensedDaton(this.dataDictionary!, condensedDaton)
        }
        if (!daton) throw new Error('Daton missing'); //if this happens, there is a code bug

        //cache it if subscribing
        if (options.doSubscribe) {
            this.persistonCache[datonKey] = daton;
            const oldSubscribeLevel = this.subscribeLevel[datonKey];
            if (!oldSubscribeLevel)
                this.subscribeLevel[datonKey] = 1;
            this.versionCache[datonKey] = daton.version;

            //clone it so caller cannot clobber our nice cached version
            const parsedKey = DatonKey.parse(datonKey);
            const datondef = Utils.getDatonDef(this.dataDictionary!, parsedKey.typeName);
            if (!datondef) throw new Error('Unknown type name ' + parsedKey.typeName);
            daton = CloneTool.clone(datondef, daton);
        }
        
        return daton;
    }

    //get subscription state by daton key (see changeSubscriptionState for meaning of values)
    getSubscriptionState(datonkey: string): 0|1|2 {
        return this.subscribeLevel[datonkey] || 0;
    }

    //change the subscription/lock state of one or more previously-cached persistons
    //to 0 (forgotten), 1 (cached and subscribed), or 2 (cached and locked).
    //returns object with error codes indexed by daton key
    async changeSubscribeState(datons: any[], subscribeState:0|1|2): Promise<any> {
        const requestDetails: any = [];
        for (const daton of datons) {
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
                if (!pristine || !subscribeLevel) throw new Error('daton cannot be saved because it was not cached');
            }
            
            //determine if lock needed
            if (pristine && subscribeLevel === 1) datonsToLock.push(pristine);

            //generate diff
            const datondef = Utils.getDatonDef(this.dataDictionary!, datonkey.typeName);
            if (!datondef) throw new Error('invalid type name');
            const diff = DiffTool.generate(datondef, pristine, modified); //pristine is falsy for new single-main-row persistons here
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

        //unlock whatever we locked
        if (datonsToLock.length) {
            await this.changeSubscribeState(datonsToLock, 1);
        }

        //error reporting
        const ret = { success: saveResponse?.savePersistonsSuccess || false, details: saveResponse?.savedPersistons || []};
        return ret;
    }

    private async callInitialize() {
        const request = { sessionKey: this.sessionKey, initialze: { languageCode: this.languageCode}};
        const response = await Utils.httpMain(this.baseServerUrl(), request);
        if (response && response.dataDictionary)
            this.dataDictionary = response.dataDictionary;
    }
    
    private baseServerUrl(): string {
        return this.serverList[this.serverIdx];
    }

    private ensureInitialized() {
        if (!this.dataDictionary) throw new Error('Session not initialized'); 
    }

    // private longPoll(): void {

    // }
}