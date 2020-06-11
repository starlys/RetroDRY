import React from 'react';
import ReactDOM from 'react-dom';
import './index.css';
import App from './App';
import globals from './globals';
import { Session } from 'retrodry';
import { sampleLayouts } from './constants';

function mainRender() {
    ReactDOM.render(<App />, document.getElementById('root'));
}

/*
Your actual client side app will need to log in somewhere in a manner similar to this function.
You will obtain the session key through your own means from the server, then set members of
Session, then call Session.start(). Your app will likely have other features that don't use retroDRY
so you will code the initialization sequence somewhere else - not in index.js.
 */
(async function() {
    mainRender();
    const apiUrl = 'https://localhost:5001/api/';
    const newSessionResponse = await fetch(apiUrl + 'test/newsession/0,buffy');
    const sessionKey = (await newSessionResponse.json()).sessionKey;
    const ses = new Session();
    ses.sessionKey = sessionKey;
    ses.serverList = [apiUrl];
    ses.timeZoneOffset = -4 * 60;
    await ses.start();
    if (ses.dataDictionary) 
        globals.session = ses;
    else
        console.log('Could not start retrodry session');
    ses.registerCardLayout('Customer', 'customer', sampleLayouts.customerCard);
    ses.registerCardLayout('CustomerList', 'customer', sampleLayouts.customerListCard);
    ses.registerCardLayout('CustomerList', 'criteria', sampleLayouts.customerListCriteriaCard);
    ses.registerGridLayout('CustomerList', 'customer', sampleLayouts.customerListGrid);
    mainRender();
})();


