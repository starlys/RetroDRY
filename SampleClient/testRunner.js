const integrationTestContainer = {
    sampleApi: 'https://localhost:5001/api/',

    //data that lasts between test steps
    session1Key: '',
    session1: null,
    widgetCoId: 0,
    widgetCo: null,

    //entry point - quick tests
    runTestSuite: async function() {
        let stepCode = '10-10';
        while (true) {
            stepCode = await this.getNextStep(stepCode);
            const found = await this.doStep(stepCode);
            if (!found) break;
        }
    },
    
    //entry point - slow tests
    runSlowTestSuite: async function() {
        let stepCode = '1010-10';
        while (true) {
            stepCode = await this.getNextStep(stepCode);
            const found = await this.doStep(stepCode);
            if (!found) break;
        }
    },

    getNextStep: async function(stepCode) {
        const responseRaw = await fetch(this.sampleApi + 'test/nextaction/' + stepCode);
        const response = await responseRaw.json();
        if (response.validateError) throw new Error('Server failed: ' + response.validateError);
        return response.nextStepCode;
    },
    
    doStep: async function(stepCode) {
        const action = this['step' + stepCode];
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

    //each step is a member of this object returning a promise; 
    //see server side code for documentation on sequence

    // ****************************************************************************
    // ***************************** QUICK TEST STEPS *****************************
    // ****************************************************************************

    'step10-20': async function() {
        const badSession = await retrodry.start([this.sampleApi], 'nosuchsessionkey', -5 * 60);
        if (badSession) this.fail('Expected no session on bad credentials');
    },
    
    'step10-30': async function() {
        const newSessionResponse = await fetch(this.sampleApi + 'test/newsession');
        this.session1Key = (await newSessionResponse.json()).sessionKey;
        const session = await retrodry.start([this.sampleApi], this.session1Key, -5 * 60);
        if (!session) this.fail('Expected successful session start');
        const numDatons = session.dataDictionary.datonDefs.length;
        if (numDatons !== 9) this.fail('Wrong number of datons in data dict');
        this.session1 = session;
    },
    
    'step20-10': async function() {
        const customerList = await this.session1.get('CustomerList');
        const numCustomers = customerList.customer.length;
        if (numCustomers !== 0) this.fail('Expected no customers');
    },
    
    'step30-10': async function() {
        //add rows to PhoneType and save
        const phoneTypes = await this.session1.get('PhoneTypeLookup', { doSubscribe: true });
        phoneTypes.phoneType.push({phoneTypeId: -1, typeOfPhone: 'Office'});
        phoneTypes.phoneType.push({phoneTypeId: -1, typeOfPhone: 'Cell'});
        const saveResult = await this.session1.save([phoneTypes]);
        if (!saveResult.success) this.fail('Expected to save phoneTypes');
    },
    
    'step30-20': async function() {
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
        
        //create customer
        const widgetCo = {
            key: 'Customer|=-1',
            customerId: -1,
            company: 'Widget, Inc.',
            salesRepId: 1, //sammy
            notes: 'Customer has great holliday parties'
        };
        saveResult = await this.session1.save([widgetCo]);
        if (!saveResult.success) this.fail('Expected to save customer');
    },
    
    'step40-10': async function() {
        const custList = await this.session1.get('CustomerList', { forceCheckVersion: true });
        if (!custList.key) this.fail('Expected CustomerList to be a daton');
        const custRows = custList.customer;
        const numCustomers = custRows.length;
        if (numCustomers !== 1) this.fail('Expected one customer');
        const widgetCo = custRows.find(el => el.company == 'Widget, Inc.');
        if (!widgetCo) this.fail('Expected company not in Customers');
        this.widgetCoId = widgetCo.customerId;   
    },
    
    'step50-10': async function() {
        //lock customer
        const widgetKey = 'Customer|=' + this.widgetCoId;
        this.widgetCo = await this.session1.get(widgetKey, { doSubscribe: true });
        if (!this.widgetCo) this.fail('Cannot load persiston WidgetCo');
        //this.widgetCo is a clone of the locally cached version
        const stateErrors = await this.session1.changeSubscribeState([this.widgetCo], 2);
        if (stateErrors[widgetKey]) this.fail('Error locking WidgetCo: ' + stateErrors[widgetKey]);
    },
    
    'step60-10': async function() {
        //unlock customer w/o changes
        const stateErrors = await this.session1.changeSubscribeState([this.widgetCo], 1);
        if (stateErrors[this.widgetCo.key]) this.fail('Error unlocking WidgetCo: ' + stateErrors[widgetKey]);
    },

    'step70-10': async function() {
        //edit employee and child contact record and save, then unlock
        const sammy = await this.session1.get('Employee|=1', { doSubscribe: true });
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


    // ****************************************************************************
    // ***************************** SLOW TEST STEPS ******************************
    // ****************************************************************************

    'step1010-10': async function() {

    }
};
