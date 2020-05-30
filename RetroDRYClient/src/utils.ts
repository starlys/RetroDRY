import { DataDictionaryResponse, DatonDefResponse, MainRequest, MainResponse } from "./wireTypes";

export default class Utils {
    //typed http post with json-decoded response
    //example consuming code: const response = await httpPost<MyReturnType>("https://...", body);
    static async httpPost<T>(request: RequestInfo, body: any): Promise<T> { 
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
        if (!response.parsedBody) throw new Error('JSON parse error');
        return response.parsedBody;
    }

    //shorthand for calling httpPost for MainRequest/MainResponse
    static async httpMain(baseServerUrl: string, request: MainRequest): Promise<MainResponse> {
        const response = await Utils.httpPost<MainResponse>(baseServerUrl + 'retro/main', request);
        return response;
    }

    //find the daton definition by daton type name
    static getDatonDef(databaseDef: DataDictionaryResponse, typeName: string): DatonDefResponse|undefined {
        return databaseDef.datonDefs.find(d => d.name === typeName); 
    }
}

export interface HttpResponse<T> extends Response {
    parsedBody?: T;
}
