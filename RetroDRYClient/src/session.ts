import { DataDictionaryResponse } from "./wireTypes";
import { Retrovert } from "./retrovert";
import DiffTool from "./diffTool";
import DatonKey from "./datonKey";
import SaveInfo from "./saveInfo";
import Utils from "./utils";

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

    //get single daton from cache or server; null if error
    async get(datonKey: string): Promise<any> {
        this.ensureInitialized();

        //get from cache
        let daton: any = this.persistonCache[datonKey];
        if (daton) return daton;

        //get from server
        const datonRequest = {
            key: datonKey,
            doSubscribe: false, //todo
            knownVersion: null //todo
        };
        const request = { 
            sessionKey: this.sessionKey, 
            getDatons: [datonRequest]
        };
        const response = await Utils.httpMain(this.baseServerUrl(), request);
        const isOk = response && response.condensedDatons && response.condensedDatons.length == 1;
        if (!isOk) return null;
        const condensedDaton = response?.condensedDatons?.[0];

        //convert condensed to full object
        daton = Retrovert.expandCondensedDaton(this.dataDictionary!, condensedDaton)

        //cache it
        //todo
        if (true) {
            //only if subscribing? what is the client contract here
            this.persistonCache[datonKey] = daton;
            this.subscribeLevel[datonKey] = 1;

            //todo clone only when caching
        }
        
        return daton;
    }

    //save any number of datons in one transaction; the objects passed in should be abandoned by the caller after a successful save;
    //returns savePersistonResponse for each daton, and an overall success flag
    async save(datons: any[]): Promise<SaveInfo> {
        this.ensureInitialized();
        const diffs: any[] = [];
        for(let idx = 0; idx < datons.length; ++idx) {
            const modified = datons[idx];
            if (!modified.key) throw new Error('daton.key required');

            //get pristine version
            const pristine = this.persistonCache[modified.key];
            if (!pristine) throw new Error('daton cannot be saved because it was not cached');
            
            //generate diff
            const datonkey  = DatonKey.parse(modified.key);
            const datondef = Utils.getDatonDef(this.dataDictionary!, datonkey.typeName);
            if (!datondef) throw new Error('invalid type name');
            const diff = DiffTool.generate(datondef, pristine, modified);
            diffs.push(diff);
        }

        //save on server
        const request = {
            sessionKey: this.sessionKey,
            saveDatons: diffs
        };
        const response = await Utils.httpMain(this.baseServerUrl(), request);

        //error reporting
        const ret = { success: response?.savePersistonsSuccess || false, details: response?.savedPersistons || []};
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