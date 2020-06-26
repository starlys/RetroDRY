import { ColDefResponse, TableDefResponse } from "./wireTypes";
import DatonKey from "./datonKey";
import Session from "./session";

//state object supporting the UI's select dropdown and lookup button. The object can be instantiated
//for each control instance, and stores the dropdown content
export default class DropdownState {
    //set in ctor:
    session: Session;
    row: any;
    colDef: ColDefResponse;

    //set by build():
    isDynamic: boolean = false; //true if the dropdown contents need to rebuild based on other values in the row
    initialized: boolean = false;
    useLookupButton: boolean = false; //caller should inspect to determine control type
    useDropdown: boolean = false; //caller should inspect to determine control type
    dropdownRows: any[] = []; //caller should inspect to determine control contents
    dropdownValueCol?: string; //caller should inspect to determine control contents
    dropdownDisplayCols?: string[]; //caller should inspect to determine control contents

    private priorCriValue: any;

    constructor(session: Session, row: any, colDef: ColDefResponse) {
        this.session = session;
        this.row = row;
        this.colDef = colDef;
    }

    //build or rebuild - call this on each render
    //return value is true if the dropdown content changed and it should re-render
    async build(): Promise<boolean> {
        //short circuit rebuilds
        if (this.initialized && !this.isDynamic) return false;
        const firstBuild = !this.initialized;
        this.initialized = true;
        let anyChanges = firstBuild;

        if (this.colDef.selectBehavior) {

            //use dropdown loaded with the contents of a viewon
            if (this.colDef.selectBehavior.useDropdown) {
                const filters = [];
                let criValueChanged = false;
                if (this.colDef.selectBehavior.autoCriterionName && this.colDef.selectBehavior.autoCriterionValueColumnName) {
                    this.isDynamic = true;
                    let name = this.colDef.selectBehavior.autoCriterionName;
                    name = name[0].toUpperCase() + name.substr(1);
                    let criValue = this.row[this.colDef.selectBehavior.autoCriterionValueColumnName];
                    if (criValue === undefined || criValue === null) criValue = '-1';
                    if (criValue !== this.priorCriValue) criValueChanged = true;
                    this.priorCriValue = criValue;
                    filters.push(name + '=' + criValue);
                }
                const parsedViewonKey = new DatonKey(this.colDef.selectBehavior.viewonTypeName, filters);
                const viewonKey = parsedViewonKey.toKeyString();
                const selectDef = this.session.getDatonDef(this.colDef.selectBehavior.viewonTypeName);
                if ((firstBuild || criValueChanged) && selectDef && selectDef.multipleMainRows) {
                    anyChanges = true;
                    const viewon = await this.session.get(viewonKey, {shortCache: true});
                    this.useDropdown = true;
                    this.dropdownRows = viewon[selectDef.mainTableDef.name];
                    this.dropdownValueCol = selectDef.mainTableDef.primaryKeyColName;
                    this.dropdownDisplayCols = this.getDropdownColumns(selectDef.mainTableDef);
                }
            }

            //use lookup button 
            else {
                this.useLookupButton = true;
            }
        } 
        
        //if select behavior wasn't specified in metadata but a foreign key was, then assume that the foreign
        //key is to a whole-table persiston and show a dropdown of the contents
        else if (this.colDef.foreignKeyDatonTypeName) {
            const selectDef = this.session.getDatonDef(this.colDef.foreignKeyDatonTypeName);
            if (selectDef && selectDef.multipleMainRows) {
                const wholeTable = await this.session.get(this.colDef.foreignKeyDatonTypeName + '|+', {doSubscribe: true, forceCheckVersion: false});
                this.useDropdown = true;
                this.dropdownRows = wholeTable[selectDef.mainTableDef.name];
                this.dropdownValueCol = selectDef.mainTableDef.primaryKeyColName;
                this.dropdownDisplayCols = this.getDropdownColumns(selectDef.mainTableDef);
            }
        }
        return anyChanges;
    }

    //wrapper to build() taking the row value and returning the display value
    async getDisplayValue(value: any): Promise<string|null> {
        await this.build();
        if (this.useDropdown) {
            const matchRows = this.dropdownRows.filter(r => r[this.dropdownValueCol!] === value);
            if (matchRows.length > 0) return matchRows[0][this.dropdownDisplayCols![0]];
        }
        return null;
    }

    //Get the columns to be shown in a dropdown 
    getDropdownColumns(tableDef: TableDefResponse) {
        const ret = [];
        const mainCol = tableDef.cols.find(c => c.isMainColumn);
        if (mainCol) ret.push(mainCol.name);
        for (let secondaryCol of tableDef.cols.filter(c => c.isVisibleInDropdown))
            ret.push(secondaryCol.name);
        return ret;
    }
}