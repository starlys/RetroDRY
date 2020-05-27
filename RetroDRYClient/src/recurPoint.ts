import { TableDefResponse, DatonDefResponse } from "./wireTypes"

// Base class for the functionality to recurse over all rows and child tables in a daton.
export class RecurPoint {
    tableDef: TableDefResponse;

    constructor(tabledef: TableDefResponse) {
        this.tableDef = tabledef;
    }

    // Get a child table within a parent row 
    protected static getChildTable(parent: any, f: string, createIfMissing: boolean): any[] {
        let list = parent[f];
        if (list) return list;
        if (createIfMissing) parent[f] = [];
        return parent[f];
    }
}

// RecurPoint for a table
export class TableRecurPoint extends RecurPoint {
    table: any[];

    // Create a TableRecurPoint for the top level of a daton having multiple main rows
    static FromDaton(datondef: DatonDefResponse, daton: any): TableRecurPoint
    {
        return new TableRecurPoint(datondef.mainTableDef, RecurPoint.getChildTable(daton, datondef.mainTableDef.name, true));
    }

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

    getChildren(): TableRecurPoint[] {
        if (this.tableDef.children) {
            return this.tableDef.children.map(childTableDef => new TableRecurPoint(childTableDef, RecurPoint.getChildTable(this.tableDef, childTableDef.name, true)));
        }
        return [];
    }
}
