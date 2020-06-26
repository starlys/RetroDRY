export default interface GetOptions {
    //if true, the persiston is cached and subscribed, else the subscription is unchanged
    //(persistons only)
    doSubscribe?: boolean;

    //if true,  the caller may edit the returned persiston and save it; 
    //if false then the copy returned to the caller should be treated as read only and temporary
    //(persistons only; implicitly sets doSubscribe)
    isForEdit?: boolean;

    //if true, the daton is cached short term; applicable to viewons and not compatible with doSubscribe flag
    shortCache?: boolean;

    //if true, forces a server check even if the daton is cached locally
    forceCheckVersion?: boolean;
}