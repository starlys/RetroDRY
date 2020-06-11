import { DatonDefResponse } from "./wireTypes";
import { RowRecurPoint, TableRecurPoint, TableRecurPointFromDaton } from "./recurPoint";

//utility for cloning datons
export default class CloneTool {
    static clone(datonDef: DatonDefResponse, daton0: any): any {
        const daton1: any = {
            key: daton0.key,
            version: daton0.version
        };
        daton1.isComplete = daton0.isComplete !== false

        if (datonDef.multipleMainRows) {
            const rt0 = TableRecurPointFromDaton(datonDef, daton0);
            const rt1 = TableRecurPointFromDaton(datonDef, daton1);
            CloneTool.cloneTable(rt0, rt1);
        } else {
            const r0 = new RowRecurPoint(datonDef.mainTableDef, daton0);
            const r1 = new RowRecurPoint(datonDef.mainTableDef, daton1);
            CloneTool.cloneRow(r0, r1);
        }
        return daton1;
    }

    //modifies r1.row; recursive
    private static cloneRow(r0: RowRecurPoint, r1: RowRecurPoint) {
        //clone values in this row
        for (const coldef of r0.tableDef.cols) {
            const value0 = r0.row[coldef.name];
            r1.row[coldef.name] = value0;
        }

        //clone child tables
        const children0 = r0.getChildren();
        const children1 = r1.getChildren();
        for (let ctidx = 0; ctidx < children0.length; ++ ctidx) {
            const ct0 = children0[ctidx];
            const ct1 = children1[ctidx];
            this.cloneTable(ct0, ct1);
        }
    }

    //modifies rt1.table; recursie
    private static cloneTable(rt0: TableRecurPoint, rt1: TableRecurPoint): any {
        for (const r0 of rt0.getRows()) {
            const row1 = {};
            rt1.table.push(row1);
            const r1 = new RowRecurPoint(rt1.tableDef, row1);
            this.cloneRow(r0, r1);
        }
    }
}