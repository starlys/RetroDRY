import { DatonDefResponse } from "./wireTypes";
import { RowRecurPoint, TableRecurPoint, TableRecurPointFromDaton } from "./recurPoint";

//utility for creating diffs 
export default class DiffTool {
    
    //generate a diff for deletion of an entire single-main-row persiston
    static generateDiffForDelete(datonDef: DatonDefResponse, daton: any) {
        if (datonDef.multipleMainRows) throw new Error('Can only delete single-main-row persistons');
        const diff: any = {
            key: daton.key,
            version: daton.version
        };
        const pkName = datonDef.mainTableDef.primaryKeyColName;
        if (!pkName) throw new Error('Missing primary key');
        const delDiff: any = {};
        delDiff[pkName] = daton[pkName];
        diff[datonDef.mainTableDef.name + '-deleted'] = [delDiff];
        return diff;
    }

    //generate a diff in the wire format required by the server by comparing the pristine and modified versions
    //of a daton; or return null if there are no changes;
    //if pristine is missing, creates diff of all values in modified (used for creating new persistons)
    static generate(datonDef: DatonDefResponse, pristine: any, modified: any): any {
        const diff: any = {
            key: modified.key,
            version: pristine?.version
        };
        let hasChanges = false;
        if (datonDef.multipleMainRows) {
            if (pristine) {
                const rt0 = TableRecurPointFromDaton(datonDef, pristine);
                const rt1 = TableRecurPointFromDaton(datonDef, modified);
                const topDiffs = DiffTool.diffTable(rt0, rt1);
                for (const key in topDiffs) {
                    diff[key] = topDiffs[key];
                    hasChanges = true;
                }
            } else {
                throw new Error('Cannot create diff for new periston with multiple main rows; must load and modify existing persiston');
            }
        } else { //single main row
            const r1 = new RowRecurPoint(datonDef.mainTableDef, modified);
            if (pristine) {
                const r0 = new RowRecurPoint(datonDef.mainTableDef, pristine);
                const topDiff = DiffTool.diffRow(r0, r1);
                if (topDiff) {
                    diff[r0.tableDef.name] = [topDiff];
                    hasChanges = true;
                }
            } else {
                //special case of new persiton with single main row
                const topDiff: any = {};
                for (const coldef of r1.tableDef.cols) {
                    topDiff[coldef.name] = r1.row[coldef.name];
                }
                DiffTool.addInsertedRowChildren(topDiff, r1);
                diff[r1.tableDef.name + '-new'] = [topDiff];
                hasChanges = true;
            }
        }
        if (hasChanges) return diff;
        return null;
    }

    //modifies rowDiff to include all child rows as inserted, recusively
    private static addInsertedRowChildren(rowDiff: any, r1: RowRecurPoint) {
        for (const rt1 of r1.getChildren()) {
            const insertedRows = DiffTool.getInsertedRows(rt1);
            if (insertedRows.length) {
                rowDiff[rt1.tableDef.name + '-new'] = insertedRows;
            }
        }
    }
    
    //gets all rows as diff objects in an inserted context (assumes every value changed); recurs to child tables
    private static getInsertedRows(rt1: TableRecurPoint): any[] {
        const ret: any[] = [];
        for (const r1 of rt1.getRows()) {
            const diff: any = {};
            for (const coldef of r1.tableDef.cols) {
                diff[coldef.name] = r1.row[coldef.name];
            }
            DiffTool.addInsertedRowChildren(diff, r1);
            ret.push(diff);
        }
        return ret;
    }

    //given two rows where the caller has determined the primary keys are the same, return a diff object with the changed values
    //or null if there are no changes; ignores primary key
    private static diffRow(r0: RowRecurPoint, r1: RowRecurPoint) {
        const diff: any = {};
        let hasChanges = false;

        //diff values in this row
        for (const coldef of r0.tableDef.cols) {
            const value0 = r0.row[coldef.name];
            const value1 = r1.row[coldef.name];
            if (value0 !== value1) {
                diff[coldef.name] = value1;
                hasChanges = true;
            }
        }

        //diff child tables
        const children0 = r0.getChildren();
        const children1 = r1.getChildren();
        for (let ctidx = 0; ctidx < children0.length; ++ ctidx) {
            const ct0 = children0[ctidx];
            const ct1 = children1[ctidx];
            const childDiffs = DiffTool.diffTable(ct0, ct1);
            for (const key in childDiffs) {
                diff[key] = childDiffs[key];
                hasChanges = true;
            }
        }

        if (hasChanges) return diff;
        return null;
    }

    //given two tables of the same type and hierarchy level, return its diff table entries.
    //it may return an object with zero members if no changes, or up to three members (TABLENAME, TABLENAME-new, TABLENAME-deleted),
    //where each member contains an array of diff rows
    private static diffTable(rt0: TableRecurPoint, rt1: TableRecurPoint): any {
        const pkName = rt0.tableDef.primaryKeyColName;
        if (!pkName) throw new Error('Cannot diff without primary key');
        const modified: any[] = [];
        const deleted: any[] = [];
        const inserted: any[] = [];

        //store all RowRecurPoints
        const rows0: RowRecurPoint[] = [];
        const rows1: RowRecurPoint[] = [];
        for (const r0 of rt0.getRows()) rows0.push(r0);
        for (const r1 of rt1.getRows()) rows1.push(r1);

        //for each pristine row, see if it was deleted or changed
        for (const r0 of rows0) {
            const pk0 = r0.row[pkName];
            const r1Idx: any = rows1.findIndex(r => r.row[pkName] === pk0);
            if (r1Idx === -1) { //was deleted
                const rowDiff: any = {};
                rowDiff[pkName] = pk0;
                deleted.push(rowDiff);
            } else { //was possibly changed
                const dr = DiffTool.diffRow(r0, rows1[r1Idx]);
                if (dr) {
                    dr[pkName] = pk0;
                    modified.push(dr);
                }
                rows1.splice(r1Idx, 1);
            }
        }

        //each remaining row in rows1 was inserted
        for (const r1 of rows1) {
            const rowDiff: any = {};
            for (const coldef of r1.tableDef.cols) {
                rowDiff[coldef.name] = r1.row[coldef.name];
            }
            DiffTool.addInsertedRowChildren(rowDiff, r1);
            inserted.push(rowDiff);
        }

        //bundle into return value
        const ret: any = {};
        if (inserted.length) {
            ret[rt0.tableDef.name + '-new'] = inserted;
        }
        if (modified.length) {
            ret[rt0.tableDef.name] = modified;
        }
        if (deleted.length) {
            ret[rt0.tableDef.name + '-deleted'] = deleted;
        }
        return ret;
    }
}