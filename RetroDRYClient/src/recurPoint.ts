import { TableDefResponse, DatonDefResponse } from "./wireTypes"

// Base class for the functionality to recurse over all rows and child tables in a daton.
export class RecurPoint {
    tableDef: TableDefResponse;

    constructor(tabledef: TableDefResponse) {
        this.tableDef = tabledef;
    }

    // Get a RecurPoint for the top level of a daton, which will be a RowRecurPoint for single-main-row type, else a TableRecurPoint
    static FromDaton(datondef: DatonDefResponse, daton: any): RecurPoint
    {
        if (datondef.multipleMainRows) 
            return new TableRecurPoint(datondef.mainTableDef, RecurPoint.getChildTable(daton, datondef.mainTableDef.name, true));
        else
            return new RowRecurPoint(datondef.mainTableDef, daton);
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
class TableRecurPoint extends RecurPoint {
    table: any[];

    constructor(tableDef: TableDefResponse, table: any[]) {
        super(tableDef);
        this.table = table;
    }

    *getRows(): IterableIterator<RowRecurPoint> {
        for (let row of this.table)
            yield new RowRecurPoint(this.tableDef, row);
    }
}

// RecurPoint for a row
class RowRecurPoint extends RecurPoint {
    row: any;

    constructor(tableDef: TableDefResponse, row: any) {
        super(tableDef);
        this.row = row;
    }

    *getChildren(): IterableIterator<TableRecurPoint> {
        if (this.tableDef.children) {
            for (let childTableDef of this.tableDef.children)
            {
                yield new TableRecurPoint(childTableDef, RecurPoint.getChildTable(this.tableDef, childTableDef.name, true));
            }
        }
    }
}
