import { TableDefResponse } from "./wireTypes";

export class LayoutTool {

}

//layout for a card
export class PanelLayout {
    //optional label for the group of inputs
    label?: string;

    //true to include visual border on group
    border?: boolean = false;
    
    //true to layout as horizontal flow; false or omitted for vertical
    horizontal?: boolean = false;

    //css class names to add to panel
    classNames?: string;

    //each element is single input or nested panel; if it is a string, it is the name of the column.
    //Or it can be a series of space-separated names which causes the first to include the prompt and the others
    //to appear in compact form after it
    content?: (string|PanelLayout)[];

    static autoGenerate(tabledef: TableDefResponse): PanelLayout {
        const panel = new PanelLayout();
        panel.content = tabledef.cols.map(c => c.name);
        return panel;
    }
}

//layout for a grid
export class GridLayout {
    columns: GridColumnLayout[] = [];

    static autoGenerate(tabledef: TableDefResponse): GridLayout {
        const grid = new GridLayout();
        grid.columns = tabledef.cols.map(c => new GridColumnLayout(c.name));
        return grid;
    }
}

export class GridColumnLayout {
    //width in ems
    width: number = 10;

    //column name
    name?: string;

    constructor(name: string) {
        this.name = name;
    }
}