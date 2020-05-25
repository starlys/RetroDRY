export default class Utils {
    //typed http post with json-decoded response
    //example consuming code
    //const response = await httpPost<Todo[]>("https://...", body);
    static async httpPost<T>(request: RequestInfo, body: any): Promise<T | null> { //todo: was: Promise<HttpResponse<T>>
        const args: RequestInit = { 
            method: "post", 
            body: JSON.stringify(body),
            mode: 'cors',
            headers: {
                'Content-Type': 'application/json'
            }
        };
        const response: HttpResponse<T> = await fetch(request, args);
        if (!response.ok) throw new Error(response.statusText);
        response.parsedBody = await response.json();
        return response.parsedBody || null;
    }
}

export interface HttpResponse<T> extends Response {
    parsedBody?: T;
}
