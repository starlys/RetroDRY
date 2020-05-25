//The js code here just demonstrates how to use RetroDRY as concisely as possible; 
//in a real app, you will probably use classes to separate concerns.

sampleClient = {
    userId: 'sandy',
    password: 'beaches',
    sessionKey: '',
    session: null,
    startSession: async () => {
        //initialize framework
        sampleClient.session = await retrodry.start(['https://localhost:5001/'], sampleClient.sessionKey, sampleClient.userId, sampleClient.password, -5 * 60);
    }
};

