//sample data and definitions to use with unit tests
export class Samples {
    static widgetDef: any = {
        name: 'Widget',
        multipleMainRows: false,
        mainTableDef: {
            name: 'Widget',
            primaryKeyColName: 'WidgetId',
            cols: [
                {
                    name: 'WidgetId',
                    wireType: 'int32'
                },
                {
                    name: 'ItemCode',
                    wireType: 'string',
                    minLength: 1,
                    maxLength: 100
                },
                {
                    name: 'Notes',
                    wireType: 'string',
                    minLength: 0,
                    maxLength: 100
                }
            ]
        }
    };
    static customerDef: any = {
        name: 'Customer',
        multipleMainRows: false,
        mainTableDef: {
            name: 'Customer',
            primaryKeyColName: 'CustomerId',
            cols: [
                {
                    name: 'CustomerId',
                    wireType: 'int32'
                },
                {
                    name: 'Name',
                    wireType: 'string',
                    minLength: 1,
                    maxLength: 100
                },
                {
                    name: 'DateCreated',
                    wireType: 'date'
                }
            ],
            children: [
                {
                    name: 'CustomerNote',
                    primaryKeyColName: 'CustomerNoteId',
                    cols: [
                        {
                            name: 'CustomerNoteId',
                            wireType: 'int32'
                        },
                        {
                            name: 'Note',
                            wireType: 'string'
                        },
                        {
                            name: 'Severity',
                            wireType: 'int16'
                        },
                        {
                            name: 'CreatedBy',
                            wireType: 'string'
                        }
                    ]
                }
            ]
        }
    };
    static dataDictionary: any = {
        datonDefs: [
            Samples.widgetDef
        ]
    };
}
