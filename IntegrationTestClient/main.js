//The js code here just demonstrates how to use RetroDRY programmatically, as concisely as possible; 
//in a real app, you will probably use classes/modules to separate concerns.

sampleClient = {
    session: null,

    runSamples: async function() {
        //start a session on the server, using the authentication mechanism in place in your host app
        const newSessionResponse = await fetch('https://localhost:5001/api/test/newsession/0,buffy');
        const sessionKey = (await newSessionResponse.json()).sessionKey;

        //start client side retrodry framework
        this.session = new retrodry.Session();
        this.session.sessionKey = sessionKey;
        this.session.serverList = ['https://localhost:5001/api/'];
        this.session.timeZoneOffset = -5 * 60;
        await this.session.start();

        //query the database for customer names starting with "Customer 2"
        let customerList = await this.session.get('CustomerList|Company=Customer 2', {forceCheckVersion: true});
        for (let customerRow of customerList.customer)
            console.log('Found customer ID=' + customerRow.customerId + ', Name=' + customerRow.company);

        //change the name of the first customer found and save
        if (customerList.customer.length) {
            const custId =  customerList.customer[0].customerId;
            const cust = await this.session.get('Customer|=' + custId, {isForEdit:true});
            cust.company = 'Widgets 4U';
            await this.session.save([cust]);
            console.log('Saved changes to customer ID ' + custId);
        }

        //query database for customer names starting with 'Widget'
        customerList = await this.session.get('CustomerList|Company=Widget', {forceCheckVersion: true});
        for (let customerRow of customerList.customer)
            console.log('Found customer ID=' + customerRow.customerId + ', Name=' + customerRow.company);

        //end session on server
        await this.session.quit();
    },
    
};

