//The js code here just demonstrates how to use RetroDRY programmatically, as concisely as possible; 
//in a real app, you will probably use classes/modules to separate concerns.

sampleClient = {
    userId: 'sandy',
    password: 'beaches',
    sessionKey: '',
    session: null,

    runSamples: async function() {

    },
    
    startSession: async function() {
        //initialize framework
        sampleClient.session = await retrodry.start(['https://localhost:5001/'], sampleClient.sessionKey, -5 * 60);
    }
};

