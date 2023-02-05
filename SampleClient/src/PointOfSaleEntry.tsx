import React, {useState} from 'react';
import { Session } from 'retrodryclient';
import CardView from './retro/CardView';
import { DatonStackLayer } from './retro/DatonStackState';

//sample of a different way to choose sale items
const itemSelectionPanel = (row: any, edit: boolean, layer: DatonStackLayer) => {
    const handler = (ev: any) => {
        row.itemId = parseInt(ev.target.value);
        row.extendedPrice = 3.99;
        if (row.saleItemId === 2) row.extendedPrice = 5.99;
    }
    //note that integration tests will populate saleItemId 1, but not 2 (so if you select the second item and save, it will fail)
    return <div style={{backgroundColor: 'silver'}}>
        <input type="radio" name="whichItem" value="1" onClick={handler}/> XX-1492 -- 
        <input type="radio" name="whichItem" value="2" onClick={handler}/> XX-1493
    </div>
};

//these 2 layouts are never registered with Session but can be used on an ad-hoc basis
const saleCard = {
    label: 'Sale Header',
    border: true,
    content: [
        'customerId',
        'saleDate',
        'isRushOrder'
    ],
};
const saleItemCard = {
    label: 'Item',
    content: [
        itemSelectionPanel,
        'quantity'
    ],
}

//shows a master and detail card for a new sale, outside of a datonstack instance. This
//exists to demonstrate how you can use cards and grids in more controlled settings than
//what is provided by the default stack behavior. In this case, it requires exactly one sale item.
//props.session is the Session object
interface TProps {
    session: Session;
}
const Component = (props: TProps) => {
    const {session} = props;
    const [sale, setSale] = useState<any|null>(null);
    const [messages, setMessages] = useState(['Enter the sale']);
    const [isInitialized, setInitialized] = useState(false);

    const datonDef = session.getDatonDef('Sale');

    //first time: create blank sale object
    if (!isInitialized) {
        setInitialized(true);
        session.get('Sale|=-1').then(newsale => {
            newsale.status = 2;
            newsale.customerId = 1;
            newsale.saleItem = [
                {
                    quantity: 1
                }
            ];
            setSale(newsale);
        });
    }
    if (!sale) return null;

    //event handlers
    const saveClicked = () => {
        session.save([sale]).then(saveInfo => {
            if (saveInfo.success) {
                setMessages(['Saved! You can enter another sale now']);
                setInitialized(false);
            } else {
                setMessages(saveInfo.details[0].errors ?? []);
            }
        });
    };

    if (!datonDef?.mainTableDef?.children) return null;

    return <>
        <div style={{color:'red'}}>
            {messages.map((s, idx) => <div key={idx}>{s}</div>)}
        </div>
        <CardView session={session} row={sale} datonDef={datonDef} tableDef={datonDef.mainTableDef} edit={true} overrideCard={saleCard} />
        <hr/>
        <CardView session={session} row={sale.saleItem[0]} datonDef={datonDef} tableDef={datonDef.mainTableDef.children[0]} edit={true} overrideCard={saleItemCard} />
        <button onClick={saveClicked}>Save New Sale</button>
    </>;
};

export default Component;