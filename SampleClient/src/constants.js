export const sampleLayouts = {
    customerListCard: {
        label: 'Customer',
        border: true,
        content: [
            { horizontal: true, content: ['customerId', 'company'] },
            'salesRepId salesRepLastName'
        ]
    },
    customerListCriteriaCard: {
        label: 'Search Criteria',
        border: true,
        content: [
            { horizontal: true, content: ['customerId', 'company'] }
        ]
    },
    customerListGrid: {
        columns: [
            {width: 3, name: 'customerId'},
            {width: 20, name: 'company'},
            {width: 20, name: 'company'},
            {width: 20, name: 'company'},
            {width: 20, name: 'salesRepLastName'},
        ]
    },
    customerCard: {
        content: [
            {
                label: 'Identification',
                border: true,
                content: [
                    { horizontal: true, content: ['customerId', 'company'] },
                    'salesRepId salesRepLastName'        
                ]
            },
            {
                label: 'Details',
                border: true,
                content: [
                    'notes'                    
                ]
            }
        ]
    },
    customerCard2: {
        label: 'Customer',
        border: true,
        content: [
            { horizontal: true, content: ['customerId', 'company'] },
            'salesRepId salesRepLastName',
            'notes'
        ]
    }
}