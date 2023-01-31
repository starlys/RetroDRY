import { DataDictionaryResponse, GetDatonRequest, LongResponse, DatonDefResponse, MainResponse } from "./wireTypes";
import { Retrovert } from "./retrovert";
import DiffTool from "./diffTool";
import {parseDatonKey} from "./datonKey";
import SaveInfo from "./saveInfo";
import NetUtils from "./netUtils";
import GetOptions from "./getOptions";
import CloneTool from "./cloneTool";
import { RecurPoint } from "./recurPoint";
import Mutex from "./mutex";
import CacheEntry from "./cacheEntry";
import GetInfo from "./getInfo";
import LayoutCollection from "./layoutCollection";
import { securityUtil } from "./securityUtil";

export default class Session {
    //delay in millis between end of long polling response and sending next long poll request
    //(can be lengthened for integration testing so we don't hit the browser's limit of open connections to the server)
    longPollDelay: number = 50;

    //UTC + this number of minutes = local time; caller should set this
    timeZoneOffset: number = 0;
   
    //server URLs to which this class will append 'retro/...'; caller should set this
    serverList: string[] = [];

    //optional environment string, which can be used to distinguish test and production environments, or other uses; caller may set this
    environment: string = 'prod';

    //server assigned key that must be fetched outside of RetroDRY and set here;
    //the code that sets this value should have already authenticated the user
    sessionKey: string = '';

    //empty string for the default language or a code supported by the server; caller should set this
    languageCode: string = '';

    //the data dictionary; set in start() (also contains language messages)
    dataDictionary?: DataDictionaryResponse;

    //if set, Session will call this whenever a daton is received from the server (when initiated by this client)
    onReceiveDaton?: (daton: any) => void;

    //if set, Session will call this whenever a daton is saved successfully, passing the modified local version
    onSave?: (daton: any) => void;

    //if set, Session will call this whenever a daton was received from the server because this session or another session edited it
    onSubscriptionUpdate?: (daton: any) => void;

    //set by session (only after start called) to the time of the latest keystroke; can be used by UI to handle lock timeouts
    //todo lastActivityMillis: number = 0;

    private serverIdx: number = 0;

    //cache indexed by key; cleaned on long polling intervals 
    private datonCache: {[key: string]: CacheEntry} = {};

    //registered layouts (including autogenerated ones)
    layouts: LayoutCollection = new LayoutCollection(this);

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

    //get single daton from cache or server; or undefined if not found/errors. In order to know what errors occured,
    //you need to call getMulti instead.
    async get(datonKey: string, options?: GetOptions): Promise<any> {
        const responses = await this.getMulti([datonKey], options);
        if (responses.length === 1)
            return responses[0].daton;
        return undefined;
    }

    //create a valid empty viewon locally (use this for seeding a searchable viewon for display without loading all rows)
    createEmptyViewon(datonType: string): any {
        return {
            key: datonType,
            isComplete: true
        };
    }

    //get one or more datons from cache or server; if any requested datons are not found, they will be omitted from the results
    //so it will not always return an array of the same size as the provided keys array.
    //Will throw errors on client programming bugs, but will return an object containing errors if there are server errors
    async getMulti(datonKeys: string[], options?: GetOptions): Promise<GetInfo[]> {
        this.ensureInitialized();
        if (!options) options = {};
        await this.getDatonMutex.acquire();
        try {
            return await this.getMultiSingleThread(datonKeys, options);
        } finally {
            this.getDatonMutex.release();
        }
    }

    //see getMulti
    private async getMultiSingleThread(datonKeys: string[], options: GetOptions): Promise<GetInfo[]> {
        if (options.isForEdit) options.doSubscribe = true;
        const doCache = options.doSubscribe  || options.shortCache;

        //convert datonKeys to array of {datonKey, parsedKey, daton(from cache), isPersiston, errors}
        let workItems = datonKeys.map(k => {
            const cacheEntry = this.datonCache[k];
            const ce:any = {
                datonKey: k,
                parsedKey: parseDatonKey(k),
                daton: cacheEntry?.daton,
                isPersiston: false
            };
            ce.isPersiston = ce.parsedKey.isPersiston();
            return ce;
        });

        //make list of requests for those we need get from server; validate inputs
        const datonRequests: GetDatonRequest[] = [];
        for (let workItem of workItems) {
            if (!workItem.isPersiston && options.doSubscribe) throw new Error('Can only subscribe or edit persistons');
            if (!workItem.daton || options.forceCheckVersion) {
                const isNew = workItem.parsedKey.isNew();
                const datonRequest = {
                    key: workItem.datonKey,
                    doSubscribe: !!(options.doSubscribe && !isNew),
                    forceLoad: !!options.forceCheckVersion,
                    knownVersion: workItem.daton?.version
                };
                datonRequests.push(datonRequest);
            }
        }

        //get from server
        if (datonRequests.length) {
            const request = { 
                sessionKey: this.sessionKey, 
                environment: this.environment,
                getDatons: datonRequests
            };
            const response = await NetUtils.httpMain(this.baseServerUrl(), request);
            if (!response) throw new Error('Get failed');

            //reinflate what we got back; if any daton is missing from the response, it means the known version was the most up to date
            if (response.getDatons) {
                for (let getResponse of response.getDatons) {
                    let daton: any = null;
                    if (getResponse.condensedDaton) {
                        daton = Retrovert.expandCondensedDaton(this, getResponse.condensedDaton);

                        //stick this one in the working list
                        const idxOfKey = workItems.findIndex(w => w.datonKey === daton.key);
                        if (idxOfKey < 0) throw new Error('Server returned daton key that was not requested');
                        workItems[idxOfKey].daton = daton;
                        if (workItems[idxOfKey].parsedKey.isNew())
                            securityUtil.markRowCreatedOnClient(daton);

                        if (this.onReceiveDaton) this.onReceiveDaton(daton);
                    }

                    //error
                    else {
                        const idxOfKey = workItems.findIndex(w => w.datonKey === getResponse.key);
                        if (idxOfKey >= 0) 
                            workItems[idxOfKey].errors = getResponse.errors;
                    }
                }
            }
        }

        //eliminate empty slots in the working list (for datons that were not loaded and didn't have errors)
        workItems = workItems.filter(w => !!w.daton || !!w.errors);

        //cache and optionally clone if subscribing (unless is new)
        if (doCache) {
            for (let workItem of workItems) {
                if (workItem.parsedKey.isNew()) continue;
                if (!workItem.daton) continue;

                //cache
                let cacheEntry = this.datonCache[workItem.datonKey];
                if (!cacheEntry) {
                    let expires = 0;
                    if (!workItem.isPersiston && options.shortCache) expires = Date.now() + 1000*60;
                    cacheEntry = {daton: workItem.daton, expires: expires, subscribeLevel: 1};
                    this.datonCache[workItem.datonKey] = cacheEntry;
                }

                if (workItem.isPersiston && !cacheEntry.subscribeLevel) cacheEntry.subscribeLevel = 1;

                //clone it so caller cannot clobber our nice cached version
                if (options.isForEdit) {
                    const datondef = this.getDatonDef(workItem.parsedKey.typeName);
                    if (!datondef) throw new Error('Unknown type name ' + workItem.parsedKey.typeName);
                    workItem.daton = CloneTool.clone(datondef, workItem.daton);
                }
            }
        }
        
        return workItems.map(w => {
            return {success: !!w.daton, errors: w.errors, daton: w.daton }; //see interface GetInfo
        });
    }

    //get subscription state by daton key (see changeSubscriptionState for meaning of values)
    getSubscribeState(datonkey: string): 0|1|2 {
        const cacheEntry = this.datonCache[datonkey];
        if (cacheEntry) return cacheEntry.subscribeLevel;
        return 0;
    }

    //change the subscription/lock state of one or more previously-cached persistons
    //to 0 (forgotten), 1 (cached and subscribed), or 2 (cached and locked).
    //returns object with error codes indexed by daton key
    async changeSubscribeState(datons: any[], subscribeState:0|1|2): Promise<any> {
        const requestDetails: any = [];
        for (const daton of datons) {
            if (!daton.key) throw new Error('Daton key missing');
            const cacheEntry = this.datonCache[daton.key];
            const currentState = cacheEntry ? cacheEntry.subscribeLevel : 0;
            if (currentState === 0 && subscribeState !== 0)
                throw new Error('Can only change subscribe state if initial get was subscribed');
            if (subscribeState !== currentState)
            {
                requestDetails.push({ 
                    key: daton.key, 
                    subscribeState: subscribeState,
                    version: cacheEntry?.daton.version
                });
            }
        }
        if (!requestDetails.length) return {};
        const request = {
            sessionKey: this.sessionKey,
            environment: this.environment,
            manageDatons: requestDetails
        };
        const response = await NetUtils.httpMain(this.baseServerUrl(), request);

        if (!response.manageDatons) return {};
        const ret: any = {};
        for (const responseDetail of response.manageDatons) {
            ret[responseDetail.key] = responseDetail.errorCode;
            if (responseDetail.subscribeState === 0) {
                delete this.datonCache[responseDetail.key];
            } else {
                const cacheEntry = this.datonCache[responseDetail.key];
                if (cacheEntry) cacheEntry.subscribeLevel = responseDetail.subscribeState;
            }
        }
        return ret;
    }

    //save any number of datons in one transaction; the objects passed in should be abandoned by the caller after a successful save;
    //returns savePersistonResponse for each daton, and an overall success flag, but in the case of an internal server error,
    //there may be no error messages in the return object
    async save(datons: any[]): Promise<SaveInfo> {
        this.ensureInitialized();
        const diffs: any[] = [];
        const datonsToLock: any[] = [];
        for(let idx = 0; idx < datons.length; ++idx) {
            const modified = datons[idx];
            if (!modified.key) throw new Error('daton.key required');

            //get pristine version
            const parsedkey = parseDatonKey(modified.key);
            const cacheEntry = this.datonCache[modified.key];
            let pristine = null;
            if (!parsedkey.isNew())
                pristine = cacheEntry?.daton;
            const subscribeLevel = cacheEntry ? cacheEntry.subscribeLevel : 0;
            if (!parsedkey.isNew()) {
                if (!pristine || !subscribeLevel) throw new Error('daton cannot be saved because it was not cached; you must get with isForEdit:true before making edits');
            }
            
            //determine if lock needed
            if (pristine && subscribeLevel === 1) datonsToLock.push(pristine);

            //generate diff
            const datondef = this.getDatonDef(parsedkey.typeName);
            if (!datondef) throw new Error('invalid type name');
            const diff = DiffTool.generate(datondef, pristine, modified); //pristine is falsy for new single-main-row persistons here
            if (diff)
                diffs.push(diff);
        }

        //if any need to be locked, do that before save, and recheck all are actually locked
        if (datonsToLock.length) {
            await this.changeSubscribeState(datonsToLock, 2);
            const anyunlocked = datonsToLock.some(d => this.datonCache[d.key]?.subscribeLevel !== 2);
            if (anyunlocked) throw new Error('Could not get lock');
        }

        //save on server
        let saveResponse:MainResponse;
        try {
            const request = {
                sessionKey: this.sessionKey,
                environment: this.environment,
                saveDatons: diffs
            };
            saveResponse = await NetUtils.httpMain(this.baseServerUrl(), request);
        } catch (e) {
            const isError = (x: any): x is Error => !!x.message;
            if (isError(e) && e.message === 'INTERNAL')
                saveResponse = {savePersistonsSuccess: false};
            else
                throw e;
        }

        //unlock whatever we locked (even if failed save)
        if (datonsToLock.length) {
            await this.changeSubscribeState(datonsToLock, 1);
        }

        //for each, remove from cache if no longer locked, or reload if still locked
        if (saveResponse?.savePersistonsSuccess) {
            const keysToRefetch = [];
            const cache = this.datonCache;
            for (let daton of datons) {
                const wasUnlocked = datonsToLock.indexOf(daton) >= 0;
                if (wasUnlocked)
                    delete cache[daton.key];
                else
                    keysToRefetch.push(daton.key);
            }
            if (keysToRefetch.length)
                await this.getMulti(keysToRefetch, {isForEdit: true, forceCheckVersion: true});
        }

        //host app callback
        if (this.onSave && saveResponse?.savePersistonsSuccess) {
            for (let daton of datons) this.onSave(daton);
        }

        //error reporting
        const ret = { success: saveResponse?.savePersistonsSuccess || false, details: saveResponse?.savedPersistons || []};
        return ret;
    }

    //delete a single-main-row daton, locking if needed
    //returns savePersistonResponse for each daton, and an overall success flag
    async deletePersiston(daton: any): Promise<SaveInfo> {
        this.ensureInitialized();
        const datonkey = parseDatonKey(daton.key);
        const datondef = this.getDatonDef(datonkey.typeName);
        const diff = DiffTool.generateDiffForDelete(datondef!, daton);
        const cacheEntry = this.datonCache[daton.key];
        const subscribeLevel = cacheEntry ? cacheEntry.subscribeLevel : 0;

        //if lock needed, do before save
        if (subscribeLevel !== 2) {
            await this.changeSubscribeState(daton, 2);
            const checkCacheEntry = this.datonCache[daton.key];
            if (!checkCacheEntry || checkCacheEntry.subscribeLevel !== 2) throw new Error('Could not get lock');
        }

        //save on server
        const request = {
            sessionKey: this.sessionKey,
            environment: this.environment,
            saveDatons: [diff]
        };
        const saveResponse = await NetUtils.httpMain(this.baseServerUrl(), request);

        //manage cache 
        if (saveResponse?.savePersistonsSuccess) {
            delete this.datonCache[daton.key];
        }

        //host app callback
        if (this.onSave && saveResponse?.savePersistonsSuccess) {
            this.onSave(daton);
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
        this.datonCache = {};
        this.dataDictionary = undefined;
        //todo document.removeEventListener('keydown', this.keyDownHandler);

        const request = { 
            sessionKey: this.sessionKey, 
            environment: this.environment,
            doQuit: true 
        };
        await NetUtils.httpMain(this.baseServerUrl(), request);
    }

    private async callInitialize() {
        const request = { 
            sessionKey: this.sessionKey, 
            environment: this.environment,
            initialize: { languageCode: this.languageCode}
        };
        const response = await NetUtils.httpMain(this.baseServerUrl(), request);
        if (response?.dataDictionary)
            this.dataDictionary = response.dataDictionary;
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
            if (response?.dataDictionary) this.dataDictionary = response.dataDictionary;
            if (response?.condensedDatons) {
                for (let condensed of response.condensedDatons) {
                    const daton = Retrovert.expandCondensedDaton(this, condensed);
                    const cacheEntry = this.datonCache[daton.key];
                    if (cacheEntry) cacheEntry.daton = daton;
                    if (this.onSubscriptionUpdate) this.onSubscriptionUpdate(daton);
                }
            }
        }
        catch (e) {
            //404 and other http errors can end up here
            console.log(e);
        }

        this.cleanCache()

        //restart polling indefinitely
        const delay = this.longPollDelay + (pollOk ? 0 : 20000);
        setTimeout(() => this.longPoll(), delay); 
    }

    //clean out expired items in cache
    private cleanCache() {
        const now = Date.now();
        const obsoleteKeys: string[] = [];
        for (let [datonKey, cacheEntry] of Object.entries(this.datonCache))
            if (cacheEntry.expires && cacheEntry.expires < now)
                obsoleteKeys.push(datonKey);
        for (let datonKey of obsoleteKeys)
            delete this.datonCache[datonKey];
    }
}