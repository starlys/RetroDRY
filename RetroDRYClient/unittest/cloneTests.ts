import { Samples } from './samples';
import { deepEqual } from 'assert';
import 'mocha';
import CloneTool from '../src/cloneTool';

describe('cloning', () => {

    it('should clone to identical copy', () => {
        const tesla = {
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

        let clone = CloneTool.clone(Samples.customerDef, tesla);
        deepEqual(clone, tesla);

        //now if we add spurious members to the clone and clone that, it should ignore those new members
        clone.extra = 'I am not part of the data model';
        clone = CloneTool.clone(Samples.customerDef, clone);
        deepEqual(clone, tesla);
    });
});