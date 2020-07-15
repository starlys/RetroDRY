import React from 'react';
import ReactDOM from 'react-dom';
import App from './App';
import globals from './globals';
import { Session } from 'retrodryclient';
import { sampleLayouts } from './constants';

function mainRender() {
    ReactDOM.render(<App />, document.getElementById('root'));
}

/*
Your actual client side app will need to log in somewhere in a manner similar to this function.
You will obtain the session key through your own means from the server, then set members of
Session, then call Session.start(). Your app will likely have other features that don't use retroDRY
so you might code the initialization sequence somewhere else besides in index.js.
 */
(async function() {
    mainRender();

    //start session on server
    const apiUrl = 'https://localhost:5001/api/';
    const userId = 'buffy'; //change this to 'buffy' for admin permissions, or 'spot', 'public' or 'nate' for reduced permissions
    const newSessionResponse = await fetch(apiUrl + 'test/newsession/0,' + userId);
    const sessionKey = (await newSessionResponse.json()).sessionKey;

    //start session on client
    const ses = new Session();
    ses.sessionKey = sessionKey;
    ses.serverList = [apiUrl];
    ses.timeZoneOffset = -4 * 60;
    await ses.start();
    if (ses.dataDictionary) 
        globals.session = ses;
    else
        console.log('Could not start retrodry session');

    //register layouts (for POS business context)
    ses.layouts.registerCard('Customer', 'customer', 'POS', sampleLayouts.customerCard);
    ses.layouts.registerCard('CustomerList', 'customer', 'POS', sampleLayouts.customerListCard);
    ses.layouts.registerCard('CustomerList', 'criteria', 'POS', sampleLayouts.customerListCriteriaCard);
    ses.layouts.registerGrid('CustomerList', 'customer', 'POS', sampleLayouts.customerListGrid);
    mainRender();
})();


