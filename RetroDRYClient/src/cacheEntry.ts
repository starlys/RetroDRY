export default interface CacheEntry {
    expires?: number; //time in millis when should be removed or falsy if never
    subscribeLevel: 0|1|2; //(0=unsubscribed; 1=subscribed; 2=locked)
    daton: any; 
}