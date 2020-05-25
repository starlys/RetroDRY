const integrationTestContainer = {
    sampleApi: 'https://localhost:5001/api/',
    userId: 'buffy',
    password: 'spiffy',

    //data that lasts between test steps
    session1Key: '',
    session1: null,
    widgetCoId: 0,
    widgetCo: null,

    //entry point
    runTestSuite: async function() {
        const newSessionResponse = await fetch(this.sampleApi + 'test/newsession');
        this.session1Key = (await newSessionResponse.json()).sessionKey;
        let stepCode = 'start';
        while (true) {
            stepCode = await this.getNextStep(stepCode);
            const found = await this.doStep(stepCode);
            if (!found) break;
        }
    },
    
    getNextStep: async function(stepCode) {
        const response = await fetch(this.sampleApi + 'test/nextaction/' + stepCode);
        const nextStepCode = (await response.json()).nextStepCode;
        return nextStepCode;
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
        console.log('FAILED TEST: ' + msg);
        throw new Error("Aborting tests");
    },

    //each step is a member of this object returning a promise; 
    //see server side code for documentation on sequence

    'step10-20': async function() {
        const badSession = await retrodry.start([this.sampleApi], 'nosuchsessionkey', 'nosuchuser', 'anyoldpassword', -5 * 60);
        if (badSession) this.fail('Expected no session on bad credentials');
    },
    
    'step10-30': async function() {
        const session = await retrodry.start([this.sampleApi], this.session1Key, this.userId, this.password, -5 * 60);
        if (!session) this.fail('Expected successful session start');
        const numDatons = session.dataDictionary.datonDefs.length;
        if (numDatons !== 9) this.fail('Wrong number of datons in data dict');
        this.session1 = session;
    },
    
    'step20-10': async function() {
        const allCustomers = await this.session1.get('CustomerList');
        const numCustomers = allCustomers.length;
        if (numCustomers !== 0) this.fail('Expected no customers');
    },
    
    'step30-10': async function() {
        //todo create customer
        const widgetCo = {
            key: 'Customer|=-1',
            company: 'Widget, Inc.',
            salesRepId: 1, //todo required?
            notes: 'Customer has great holliday parties'
        };
        const saveResult = this.session1.save([widgetCo]);
    },
    
    'step40-10': async function() {
        const custList = await this.session1.get('CustomerList');
        if (!custList.key) this.fail('Expected CustomerList to be a daton');
        const custRows = custList.Customer;
        const numCustomers = custRows.length;
        if (numCustomers !== 1) this.fail('Expected one customer');
        const widgetCo = custRows.find(el => el.Company == 'Widget Co');
        if (!widgetCo) this.fail('Expected company not in Customers');
        this.widgetCoId = widgetCo.CompanyId;    
    },
    
    'step50-10': async function() {
        //todo lock customer
    },
    
    'step60-10': async function() {
        //todo unlock customer w/o changes
    },

    'step70-10': async function() {
        this.widgetCo = await this.session.get('Customer|=' + this.widgetCoId);
        if (!this.widgetCo) this.fail('Cannot load persiston WidgetCo');
        const widgetCo = this.widgetCo;
        widgetCo.Description = 'Desc 2'; //todo clone and diff...
        await this.session.save(widgetCo); //todo need a way to test that only the change field was sent
    }
};
