export default class GetOptions {
    //if true, the daton is cached, and the caller may edit the returned persiston and save it; 
    //if false then the copy returned to the caller should be treated as read only and temporary
    doSubscribeEdit: boolean = false;

    //if true, forces a server check even if the daton is cached locally
    forceCheckVersion: boolean = false;
}