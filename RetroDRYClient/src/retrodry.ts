import Cache2 from './cache2';
import Session from './session';

const cache = new Cache2();

declare global {
    interface Window { retrodry: any }
}

window.retrodry = {
    version: '0.1',

    //start session: this obtains the data dictionary and begins ongoing communication;
    //returns null on failure or a session object
    start: async function(serverList: string[], sessionKey: string, userId: string, password: string, timeZoneOffset: number) : Promise<Session | null> {
        const ses = new Session();
        ses.sessionKey = sessionKey;
        ses.serverList = serverList;
        // ses.userId = userId;
        // ses.password = password;
        ses.timeZoneOffset = timeZoneOffset;
        await ses.start();
        if (ses.dataDictionary) return ses;
        return null;
    }
};