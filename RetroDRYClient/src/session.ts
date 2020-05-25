import Utils, { HttpResponse } from "./utils";
import { MainRequest, MainResponse, DataDictionaryResponse, GetDatonRequest } from "./wireTypes";
import { Retrovert } from "./retrovert";

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

    private timer: any;
    private serverIdx: number = 0;

    //pristine persistons that are subscribed or locked, indexed by key
    private persistonCache: any = {};

    //subscribe level (1=subscribed; 2=locked) indexed by persiston key
    private subscribeLevel: any = {};

    //start session; caller should check this.dataDictionary to see if it was successful
    async start(): Promise<void> {
        if (!this.serverList.length) throw new Error('server list not initialized');
        this.serverIdx = Math.floor(Math.random() * this.serverList.length); //random server
        await this.callInitialize();

        //start long polling
        //todo
    }

    //get single daton from cache or server; null if error
    async get(datonKey: string): Promise<any> {
        if (!this.dataDictionary) throw new Error('Session not initialized'); //todo reusable func

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
        const response = await Utils.httpPost<MainResponse>(this.baseServerUrl() + 'retro/main', request);
        const isOk = response && response.condensedDatons && response.condensedDatons.length == 1;
        if (!isOk) return null;
        const condensedDaton = response?.condensedDatons?.[0];

        //convert condensed to full object
        daton = Retrovert.expandCondensedDaton(this.dataDictionary, condensedDaton)

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

    //save any number of datons; the objects passed in should be abandoned by the caller after a successful save;
    //returns ? //todo
    async save(datons: any[]) {
        const diffs: any[] = [];
        for(let idx = 0; idx < datons.length; ++idx) {
            const daton = datons[idx];
            if (!daton.key) throw new Error('daton.key required');

            //todo get pristine version
            
            //todo generate diff
            const diff = {};
            diffs.push(diff);
        }

        //save on server
        const request = {
            sessionKey: this.sessionKey,
            saveDatons: diffs
        };
        const response = await Utils.httpPost<MainResponse>(this.baseServerUrl() + 'retro/main', request); //todo move main call to Utils as not generic

        //todo error reporting
        const isOk = response && response.savedPersistons;
        if (!isOk) return null; //
        //response?.savedPersistons[0].isSuccess

    }

    private async callInitialize() {
        const request = { sessionKey: this.sessionKey, initialze: { languageCode: this.languageCode}};
        const response = await Utils.httpPost<MainResponse>(this.baseServerUrl() + 'retro/main', request);
        if (response && response.dataDictionary)
            this.dataDictionary = response.dataDictionary;
    }
    
    private baseServerUrl(): string {
        return this.serverList[this.serverIdx];
    }

    private longPoll(): void {

    }
}