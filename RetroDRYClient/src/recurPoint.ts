import { TableDefResponse, DatonDefResponse } from "./wireTypes"

// Create a TableRecurPoint for the top level of a daton having multiple main rows
export function TableRecurPointFromDaton(datondef: DatonDefResponse, daton: any): TableRecurPoint
{
    return new TableRecurPoint(datondef.mainTableDef, RecurPoint.getChildTable(daton, datondef.mainTableDef.name, true));
}

// Base class for the functionality to recurse over all rows and child tables in a daton.
export class RecurPoint {
    tableDef: TableDefResponse;

    constructor(tabledef: TableDefResponse) {
        this.tableDef = tabledef;
    }

    //Recurse to find all table definitions found in this daton definition
    static getTables(datonDef: DatonDefResponse): TableDefResponse[] {
        const ret: TableDefResponse[] = [];
        this.getTablesRecursive(ret, datonDef.mainTableDef);
        return ret;
    }

    //add this and all child tabledefs to list
    private static getTablesRecursive(list: TableDefResponse[], t: TableDefResponse) {
        list.push(t);
        if (t.children) {
            for (const child of t.children) this.getTablesRecursive(list, child);
        }
    }

    // Get a child table within a parent row 
    static getChildTable(parent: any, f: string, createIfMissing: boolean): any[] {
        let list = parent[f];
        if (list) return list;
        if (createIfMissing) parent[f] = [];
        return parent[f];
    }
}

// RecurPoint for a table
export class TableRecurPoint extends RecurPoint {
    table: any[];

    constructor(tableDef: TableDefResponse, table: any[]) {
        super(tableDef);
        this.table = table;
    }

    getRows(): RowRecurPoint[] {
        return this.table.map(row => new RowRecurPoint(this.tableDef, row))
    }
}

// RecurPoint for a row
export class RowRecurPoint extends RecurPoint {
    row: any;

    constructor(tableDef: TableDefResponse, row: any) {
        super(tableDef);
        this.row = row;
    }

    //gets child table, and creates the member in the parent object as needed
    getChildren(): TableRecurPoint[] {
        if (this.tableDef.children) {
            return this.tableDef.children.map(childTableDef => new TableRecurPoint(childTableDef, RecurPoint.getChildTable(this.row, childTableDef.name, true)));
        }
        return [];
    }
}
