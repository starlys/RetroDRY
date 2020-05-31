import DiffTool from '../src/diffTool';
import { Samples } from './samples';
import { deepEqual } from 'assert';
import 'mocha';

describe('diff building', () => {

    it('should create simple diff with one change', () => {
        const pristine = { key: 'Widget|9', version: 'x', WidgetId: 9, ItemCode: 'HEXNUT', Notes: 'Has 6 sides' };
        const modified = { key: 'Widget|9', version: 'x', WidgetId: 9, ItemCode: 'HEXNUT', Notes: 'Has 7 sides' };
        const diff = DiffTool.generate(Samples.widgetDef, pristine, modified);
        deepEqual(diff, { key: 'Widget|9', version: 'x', Widget: [ {Notes: 'Has 7 sides'}]});
    });

    it('should create diff with complex changes', () => {
        
        const pristine = { 
            key: 'Customer|9', 
            version: 'x',
            CustomerId: 9, 
            Name: 'Tesla',
            DateCreated: '2018-12-31',
            CustomerNote: [
                {
                    CustomerNoteId: '1001',
                    Note: 'Usually order 7 sided nuts',
                    Severity: 3,
                    CreatedBy: 'Star'
                },
                {
                    CustomerNoteId: '1002',
                    Note: 'COD for orders under $600',
                    Severity: 2,
                    CreatedBy: 'Star'
                }
            ]
        };

        const modified = {
            key: 'Customer|9', 
            version: 'x',
            CustomerId: 9, 
            Name: 'Tesla Inc',
            DateCreated: '2018-12-31',
            CustomerNote: [
                {
                    CustomerNoteId: '1003',
                    Note: 'Added a note!',
                    Severity: 0,
                    CreatedBy: 'Star'
                },
                {
                    CustomerNoteId: '1002',
                    Note: 'COD for orders under $1',
                    Severity: 2,
                    CreatedBy: 'Star'
                }
            ]
        };

        const expectedDiff = {
            key: 'Customer|9',
            version: 'x',
            Customer: [
                {
                    Name: 'Tesla Inc',
                    CustomerNote: [
                        {
                            CustomerNoteId: '1002',
                            Note: 'COD for orders under $1'                    
                        }
                    ],
                    'CustomerNote-new': [
                        {
                            CustomerNoteId: '1003',
                            Note: 'Added a note!',
                            Severity: 0,
                            CreatedBy: 'Star'
                        }                
                    ],
                    'CustomerNote-deleted': [
                        {
                            CustomerNoteId: '1001'
                        }
                    ]        
                }
            ]
        };

        const diff = DiffTool.generate(Samples.customerDef, pristine, modified);
        deepEqual(diff, expectedDiff);
    });

});