const integrationTestContainer = {
    sampleApi0: 'https://localhost:5001/api/',
    sampleApi1: 'https://localhost:5002/api/',
    sampleApi2: 'https://localhost:5003/api/',

    //quick tests: data that lasts between test steps
    session1: null, //buffy
    session2: null, //nate
    widgetCoId: 0,
    widgetCo: null,

    //slow tests: data that lasts between steps
    msessions: [], //24 sessions as decribed in server side comments
    mdata: [], //24 objects with contents defined by steps

    //entry point - quick tests
    runTestSuite: async function() {
        let stepCode = 'a10-10';
        while (true) {
            stepCode = await this.getNextStep(stepCode);
            const found = await this.doStep(stepCode);
            if (!found) break;
        }
    },
    
    //entry point - slow tests
    runSlowTestSuite: async function() {
        let stepCode = 'b10-10';
        while (true) {
            stepCode = await this.getNextStep(stepCode);
            const found = await this.doStep(stepCode);
            if (!found) break;
        }
    },

    getNextStep: async function(stepCode) {
        const responseRaw = await fetch(this.sampleApi0 + 'test/nextaction/' + stepCode);
        const response = await responseRaw.json();
        if (response.validateError) throw new Error('Server failed: ' + response.validateError);
        return response.nextStepCode;
    },
    
    doStep: async function(stepCode) {
        const action = this[stepCode];
        if (!action) {
            console.log('Test suite completed');
            return false; 
        }
        console.log('Starting step ' + stepCode);
        await action.bind(this)();
        return true;
    },

    fail: function(msg) {
        console.log('Client failed: ' + msg);
        throw new Error("Aborting tests");
    },

    //get integer >=0 and <max
    randomInt: function(max) {
        return Math.floor(Math.random() * max);
    },

    //awaitable delay in millis
    delay: function(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    },

    //do work in each msession in parallel and complete when all are done;
    //work signature must be: work(sesIdx, session)=>Promise
    inParallel: async function(work) {
        const works = []; //promises
        let idx = 0;
        for (let session of this.msessions) works.push(work(idx++, session));
        await Promise.all(works);
    },

    //start a session and return Session
    startSession: async function(serverList, sessionKey) {
        const ses = new retrodry.Session();
        ses.sessionKey = sessionKey;
        ses.serverList = serverList;
        ses.timeZoneOffset = -5 * 60;
        await ses.start();
        return ses;
    },

    //each step is a member of this object returning a promise; 
    //see server side code for documentation on sequence

    // ****************************************************************************
    // ***************************** QUICK TEST STEPS *****************************
    // ****************************************************************************

    'a10-20': async function() {
        const badSession = await this.startSession([this.sampleApi0], 'nosuchsessionkey');
        if (badSession && badSession.dataDictionary) this.fail('Expected no session on bad credentials');
    },
    
    'a10-30': async function() {
        const newSessionResponse = await fetch(this.sampleApi0 + 'test/newsession/0,buffy');
        const sessionKey = (await newSessionResponse.json()).sessionKey;
        const session = await this.startSession([this.sampleApi0], sessionKey);
        if (!session) this.fail('Expected successful session start');
        const numDatons = session.dataDictionary.datonDefs.length;
        if (numDatons !== 10) this.fail('Wrong number of datons in data dict');
        this.session1 = session;
    },
    
    'a20-10': async function() {
        const customerList = await this.session1.get('CustomerList');
        const numCustomers = customerList.customer.length;
        if (numCustomers !== 0) this.fail('Expected no customers');
    },
    
    'a30-10': async function() {
        //add rows to PhoneType and save
        const phoneTypes = await this.session1.get('PhoneTypeLookup|+', { doSubscribeEdit: true });
        phoneTypes.phoneType.push({phoneTypeId: -1, typeOfPhone: 'Office'});
        phoneTypes.phoneType.push({phoneTypeId: -1, typeOfPhone: 'Cell'});
        const saveResult = await this.session1.save([phoneTypes]);
        if (!saveResult.success) this.fail('Expected to save phoneTypes');
    },
    
    'a30-20': async function() {
        //create employee
        const sammy = {
            key: 'Employee|=-1',
            employeeId: -1,
            firstName: 'Sammy',
            lastName: 'Snead',
            hireDate: new Date(2008, 12, 25),
            eContact: [
                {
                    contactId: -1,
                    phoneType: 1,
                    phone: '575 555 1234'    
                }
            ]
        };
        let saveResult = await this.session1.save([sammy]);
        if (!saveResult.success) this.fail('Expected to save employee');
        
        //create invalid customer
        const widgetCo = {
            key: 'Customer|=-1',
            customerId: -1,
            company: 'The company that starts with THE',
            salesRepId: 1, //sammy
            notes: 'Customer has great holliday parties'
        };
        saveResult = await this.session1.save([widgetCo]);
        if (saveResult.success) this.fail('Expected save customer to fail');
    },
    
    'a30-30': async function() {
        //create customer
        const widgetCo = {
            key: 'Customer|=-1',
            customerId: -1,
            company: 'Widget, Inc.',
            salesRepId: 1, //sammy
            notes: 'Customer has great holliday parties'
        };
        let saveResult = await this.session1.save([widgetCo]);
        if (!saveResult.success) this.fail('Expected to save customer');
    },

    'a40-10': async function() {
        const custList = await this.session1.get('CustomerList', { forceCheckVersion: true });
        if (!custList.key) this.fail('Expected CustomerList to be a daton');
        const custRows = custList.customer;
        const numCustomers = custRows.length;
        if (numCustomers !== 1) this.fail('Expected one customer');
        const widgetCo = custRows.find(el => el.company == 'Widget, Inc.');
        if (!widgetCo) this.fail('Expected company not in Customers');
        this.widgetCoId = widgetCo.customerId;   
    },
    
    'a50-10': async function() {
        //lock customer
        const widgetKey = 'Customer|=' + this.widgetCoId;
        this.widgetCo = await this.session1.get(widgetKey, { doSubscribeEdit: true });
        if (!this.widgetCo) this.fail('Cannot load persiston WidgetCo');
        //this.widgetCo is a clone of the locally cached version
        const stateErrors = await this.session1.changeSubscribeState([this.widgetCo], 2);
        if (stateErrors[widgetKey]) this.fail('Error locking WidgetCo: ' + stateErrors[widgetKey]);
    },
    
    'a60-10': async function() {
        //unlock customer w/o changes
        const stateErrors = await this.session1.changeSubscribeState([this.widgetCo], 1);
        if (stateErrors[this.widgetCo.key]) this.fail('Error unlocking WidgetCo: ' + stateErrors[widgetKey]);
    },

    'a70-10': async function() {
        //edit employee and child contact record and save, then unlock
        const sammy = await this.session1.get('Employee|=1', { doSubscribeEdit: true });
        if (!sammy) this.fail('Cannot load persiston Sammy');
        sammy.lastName = 'Smurf';
        sammy.eContact[0].phone = '505 555 1234';
        sammy.eContact.push({
            contactId: -1,
            phoneType: 2,
            phone: 'sammy@smurfland.com'
        });
        const saveResult = await this.session1.save([sammy]);
        if (!saveResult.success) this.fail('Expected to save customer');
        const stateErrors = await this.session1.changeSubscribeState([sammy], 0);
        if (stateErrors[this.widgetCo.key]) this.fail('Error unlocking Sammy: ' + stateErrors['Employee|=1']);
    },

    'a70-20': async function() {
        //re-login as Nate
        const newSessionResponse = await fetch(this.sampleApi0 + 'test/newsession/0,nate');
        const sessionKey = (await newSessionResponse.json()).sessionKey;
        const session = await this.startSession([this.sampleApi0], sessionKey);
        if (!session) this.fail('Expected successful session start');
        this.session2 = session;

        //get customer list and confirm name not visible
        const custList = await this.session2.get('CustomerList');
        const custRows = custList.customer;
        const numCustomers = custRows.length;
        if (numCustomers !== 1) this.fail('Expected one customer');
        const widgetCoRow = custRows[0];
        if (widgetCoRow.company) this.fail('Expected company name to be invisible to Nate (viewon)');

        //get customer persiston and re-confirm the same
        const widgetKey = 'Customer|=' + this.widgetCoId;
        let widgetCo = await this.session2.get(widgetKey, { doSubscribeEdit: true });
        if (!widgetCo) this.fail('Cannot load persiston WidgetCo');
        if (widgetCo.company) this.fail('Expected company name to be invisible to Nate (persiston)');
        if (!widgetCo.notes) this.fail('Expected company notes to be visible to Nate (persiston)');

        //attempt to save changes to an invalid column
        const stateErrors = await this.session2.changeSubscribeState([widgetCo], 2); 
        if (stateErrors[widgetKey]) this.fail('Error locking WidgetCo: ' + stateErrors[widgetKey]);
        widgetCo.company = "I'm not allowed to change this column";
        let saveResult = await this.session2.save([widgetCo]);
        if (saveResult.success) this.fail('Expected to be unallowed to save customer');

        //re-get widget co from local cache
        widgetCo = await this.session2.get(widgetKey, { doSubscribeEdit: true });
        if (!widgetCo.key) this.fail('Expected widgetCo to load');
        if (widgetCo.company) this.fail('Expected company name to be unchanged by the test');

        //attempt to save changes to only the valid field 
        widgetCo.notes = 'Customer canceled their holliday parties';
        saveResult = await this.session2.save([widgetCo]);
        if (!saveResult.success) this.fail('Expected to be allowed to save customer');
    },


    // ****************************************************************************
    // ***************************** SLOW TEST STEPS ******************************
    // ****************************************************************************

    'b20-10': async function() {
        //reset local data
        this.msessions = [];
        this.mdata = [];

        const newSessionResponse = await fetch(this.sampleApi1 + 'test/newsession/1,buffy');
        const sessionKey = (await newSessionResponse.json()).sessionKey;
        const session = await this.startSession([this.sampleApi1], sessionKey);

        //create SaleStatus, Item, Employee for use in later steps
        const saleStatusses = await session.get('SaleStatusLookup|+', { doSubscribeEdit: true });
        saleStatusses.saleStatus.push({ name: 'Hold' });
        saleStatusses.saleStatus.push({ name: 'Ordered' });
        saleStatusses.saleStatus.push({ name: 'Shipped' });
        const item = {
            key: 'Item|=-1',
            itemCode: 'XX-1492',
            description: 'Sailed the ocean blue'
        };
        const sammy = {
            key: 'Employee|=-1',
            firstName: 'Sammy',
            lastName: 'Snead',
            hireDate: new Date(2008, 12, 25),
        };
        const saveResult = await session.save([saleStatusses, item, sammy]);
        if (!saveResult.success) this.fail('Expected to save sale status, item');
        await session.quit();
    },

    'b20-20': async function() {
        //create all client sessions in series
        const urls = [this.sampleApi0, this.sampleApi1, this.sampleApi2];
        let urlIdx = -1;
        for (let cliIdx = 0; cliIdx < 24; ++cliIdx) {
            urlIdx = ++urlIdx % 3;
            const newSessionResponse = await fetch(urls[urlIdx] + 'test/newsession/' + urlIdx + ',buffy');
            const sessionKey = (await newSessionResponse.json()).sessionKey;
            const session = await this.startSession([urls[urlIdx]], sessionKey);
            session.longPollDelay = 5000;
            this.msessions.push(session);
        }

        //create customers and sales 
        await this.inParallel(async (sesNo, session) => {
            const customer = {
                key: 'Customer|=-1',
                company: 'Customer ' + sesNo,
                salesRepId: 1 //sammy
            };
            let saveResult = await session.save([customer]);
            if (!saveResult.details.length) 
                this.fail('Could not save customer');
            const customerKey = retrodry.parseDatonKey(saveResult.details[0].newKey);
            const customerId = customerKey.persistonKeyAsInt();
            if (!customerId) this.fail('Expected server to return valid customer ID on insert');
            const sales = [];
            for (let i = 0; i < 40; ++i)
                sales.push({
                    key: 'Sale|=-1',
                    customerId: customerId, 
                    saleDate: new Date(2020, this.randomInt(12), this.randomInt(24) + 2), //avoid timezone problems on local server
                    status: 1
                });
            await session.save(sales);
        });
    },

    'b30-10': async function() {
        //set up mdata
        for (let i = 0; i < 24; ++i) this.mdata.push({});

        //get all sales in one month
        await this.inParallel(async (sesNo, session) => {
            await this.delay((sesNo % 2) * 1000);
            const monthNo = Math.floor(sesNo / 2) + 1;
            const monthAs2Digits = String(monthNo).padStart(2, '0');
            const fromDate = '2020' + monthAs2Digits + '01';
            const toDate = '2020' + monthAs2Digits + '29';
            const sales = await session.get('SaleList|SaleDate=' + fromDate + '~' + toDate);
            this.mdata[sesNo].saleRows = sales.sale; //note that elements are rows in SaleList
        });
    },

    'b30-20': async function() {
        //NOTE: If this step fails and it takes more than 2 minutes to complete, it could be the server
        //cleaned out "old" users and is reporting an unknown session key. To work around, run the server 
        //in non-debug mode for speed, or temporarily comment out the user cleaning code in the server.

        //get and subscribe to all the sales that were queried
        await this.inParallel(async (sesNo, session) => {
            const datonKeys = this.mdata[sesNo].saleRows.map(r => 'Sale|=' + r.saleId);
            const sales = await session.getMulti(datonKeys, { doSubscribeEdit: true });
            if (!sales || sales.length !== datonKeys.length) this.fail('Got wrong number of sales back');
            this.mdata[sesNo].sales = sales;
        });

        //try to lock all the sales that we subscribed to; since another client will also try to lock the same ones,
        //one will fail 
        let totalLocks = 0;
        await this.inParallel(async (sesNo, session) => {
            const sales = this.mdata[sesNo].sales; //this array will have elements deleted if they were not locked
            const watchingSales = []; //including the sales that didn't lock
            const stateErrors = await session.changeSubscribeState(sales, 2);
            for (let i = sales.length - 1; i >= 0; --i) {
                watchingSales.push(sales[i]);
                if (stateErrors[sales[i].key]) {
                    sales.splice(i, 1);
                }
            }
            this.mdata[sesNo].sales = sales; //now has only the ones that this client locked successfully
            this.mdata[sesNo].watchingSales = watchingSales; //this has all sales for the month
            totalLocks += sales.length;
        });

        if (totalLocks != 960) this.fail('Expected 960 locks to be successful');
    },

    'b30-30': async function() {
        //change shipped date of all sales that are locked by each client
        await this.inParallel(async (sesNo, session) => {
            const sales = this.mdata[sesNo].sales; 
            for (let sale of sales) sale.shippedDate = '2020-12-24T23:00:00';
            await session.save(sales);
        });
    },

    'b40-05': async function() {
        //december clients quit cleanly, which should release their locks
        //console.log('Quitting cleanly: ' + this.msessions[23].sessionKey);
        await this.msessions[23].quit();
        //console.log('Quitting cleanly: ' + this.msessions[22].sessionKey);
        await this.msessions[22].quit();
        this.msessions.splice(22, 2);
    },

    'b40-10': async function() {
        //unlock all the sales that we edited above and keep subscription
        await this.inParallel(async (sesNo, session) => {
            await session.changeSubscribeState(this.mdata[sesNo].sales, 1);
            this.mdata[sesNo].sales = null; //once changes are saved, we should forget on the client
        });

        //wait for all subscriptions to propagate
        await this.delay(20 * 1000); 

        //confirm all sales that are not locked received update with new shipped date
        await this.inParallel(async (sesNo, session) => {
            const watchingSales = this.mdata[sesNo].watchingSales;
            for (let sale of watchingSales) {
                const sale2 = session.persistonCache[sale.key]; //using private member of session to get the updated version
                if (!sale2 || !sale2.shippedDate) 
                    this.fail('Sale being watched did not get subscription update');
            }
        });
    },

    'b40-20': async function() {
        //november clients abort uncleanly
        //console.log('Quitting uncleanly: ' + this.msessions[21].sessionKey);
        this.msessions[21].dataDictionary = undefined;
        //console.log('Quitting uncleanly: ' + this.msessions[20].sessionKey);
        this.msessions[20].dataDictionary = undefined;
        this.msessions.splice(20, 2);
    },

    'b40-30': async function() {
        await this.inParallel(async (sesNo, session) => {
            const watchingSales = this.mdata[sesNo].watchingSales;
            const errors = await session.changeSubscribeState(watchingSales, 0);
            for (let key in session.persistonCache)
                if (session.persistonCache.hasOwnProperty(key))
                    this.fail('Expected client cache to be empty');
        });
    },

    'b40-80': async function() {
        await this.inParallel(async (sesNo, session) => {
            await session.quit();
        });
    }
};
