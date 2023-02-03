import { MainRequest, MainResponse, RetroResponse } from "./wireTypes";

export default class NetUtils {
    //typed http post with json-decoded response
    //example consuming code: const response = await httpPost<MyReturnType>("https://...", body);
    static async httpPost<T extends RetroResponse>(request: RequestInfo, body: any): Promise<T> { 
        const args: RequestInit = { 
            method: "post", 
            body: JSON.stringify(body),
            mode: 'cors',
            headers: {
                'Content-Type': 'application/json'
            }
        };
        try {
            const response: HttpResponse<T> = await fetch(request, args);
            if (!response.ok) throw new Error(response.statusText);
            response.parsedBody = await response.json();
            if (!response.parsedBody) throw new Error('JSON parse error');
            return response.parsedBody;
        } catch {
            //network failure
            const errorRet: MainResponse = {errorCode: 'NET'};
            return errorRet as any;
        }
    }

    //shorthand for calling httpPost for MainRequest/MainResponse
    static async httpMain(baseServerUrl: string, request: MainRequest): Promise<MainResponse> {
        const response = await NetUtils.httpPost<MainResponse>(baseServerUrl + 'retro/main', request);
        return response;
    }

    // static async httpExport(baseServerUrl: string, eRequest: ExportRequest): Promise<any> {
    //     const args: RequestInit = { 
    //         method: "post", 
    //         body: JSON.stringify(eRequest),
    //         mode: 'cors',
    //         headers: {
    //             'Content-Type': 'application/json'
    //         }
    //     };
    //     try {
    //         const response: Response = await fetch(baseServerUrl + 'retro/export', args);
    //         if (!response.ok) throw new Error(response.statusText);
    //     } catch {
    //         //network failure
    //         console.log('Export failed');
    //     }
    // }
}

export interface HttpResponse<T> extends Response {
    parsedBody?: T;
}
