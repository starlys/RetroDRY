import { TableDefResponse } from "./wireTypes";
import {parseDatonKey} from "./datonKey";
import Session from "./session";

//utilities for type conversion
export class Retrovert {
    
    //given a condensed daton from the wire, expand it to the normal structure
    static expandCondensedDaton(session: Session, condensed: any): any {
        let daton: any = {};
        if (!condensed.key) throw new Error('Condensed daton has no key');
        daton.key = condensed.key;
        daton.isComplete = condensed.isComplete !== false; //omitted means true
        daton.version = condensed.version;
        const datonKey = parseDatonKey(daton.key);
        const datondef = session.getDatonDef(datonKey.typeName); 
        if (!datondef) throw new Error(`No type ${datonKey.typeName}`);
        if (datondef.multipleMainRows) {
            const maintargetlist: any[] = daton[datondef.mainTableDef.name] = [];
            this.expandCondensedTable(datondef.mainTableDef, condensed.content, maintargetlist);
        } else {
            this.expandCondensedRow(datondef.mainTableDef, condensed.content[0], daton);
        }
        return daton;
    }

    private static expandCondensedRow(tabledef: TableDefResponse, condensed: any[], target: any) {
        let idx = -1;
        for (const coldef of tabledef.cols) {
            const value = condensed[++idx];
            target[coldef.name] = value;
        }
        if (tabledef.children) {
            for (const childTabledef of tabledef.children) {
                const condensedRows = condensed[++idx];
                const childtargetList = target[childTabledef.name] = [];
                this.expandCondensedTable(childTabledef, condensedRows, childtargetList);
            }
        }
    }

    private static expandCondensedTable(tabledef: TableDefResponse, condensedRows: any[], targetList: any[]) {
        for (const condensedRow of condensedRows) {
            const target = {};
            targetList.push(target);
            this.expandCondensedRow(tabledef, condensedRow, target);
        }
    }
}