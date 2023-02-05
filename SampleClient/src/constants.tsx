import React from 'react';
import { PanelLayout, GridLayout } from 'retrodryclient';
import { DatonStackLayer } from './retro/DatonStackState';

//custom button behaviors
const resetCustomerNotesButton = (row: any, edit: boolean, layer: DatonStackLayer) => {
    if (!edit) return null;
    const handler = () => {
        row.notes = 'No notes';
        layer.rerender();
    }
    return <button onClick={handler}>Reset Notes</button>;
};

export const sampleGridLayouts: {[index:string]: GridLayout} = {
    customerListGrid: {
        columns: [
            {width: 3, name: 'customerId'},
            {width: 20, name: 'company'},
            {width: 20, name: 'company'},
            {width: 20, name: 'company'},
            {width: 20, name: 'salesRepLastName'},
        ],
    },
}

export const samplePanelLayouts: {[index:string]: PanelLayout} = {
    employeeCard: {
        content: ['employeeId', 'firstName', 'lastName', 'supervisorId', 'supervisorLastName', 'hireDate', 'neatDeskRating',
            {
                label: 'Red flags',
                border: true,
                content: ['isToxic'],
            }
        ],
    },
    customerListCard: {
        label: 'Customer',
        border: true,
        content: [
            { 
                horizontal: true, content: ['customerId', 'company'] ,
            },
            'salesRepId salesRepLastName'
        ],
    },
    customerListCriteriaCard: {
        label: 'Search Criteria',
        border: true,
        content: [
            { 
                horizontal: true, content: ['customerId', 'company'],
            }
        ],
    },
    customerCard: {
        content: [
            {
                label: 'Identification',
                border: true,
                content: [
                    { 
                        horizontal: true, content: ['customerId', 'company'] ,
                    },
                    'salesRepId salesRepLastName'        
                ],
            },
            {
                label: 'Details',
                border: true,
                content: [
                    'notes',
                    resetCustomerNotesButton
                ],
            }
        ],
    },
    customerCard2: {
        label: 'Customer',
        border: true,
        content: [
            { 
                horizontal: true, content: ['customerId', 'company'],
             },
            'salesRepId salesRepLastName',
            'notes'
        ],
    }
}