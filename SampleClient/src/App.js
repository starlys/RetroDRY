import React, {useState} from 'react';
import globals from './globals';
import DatonStack from './retro/DatonStack';
import DatonStackState from './retro/DatonStackState';
import PointOfSaleEntry from './PointOfSaleEntry';

import './App.css';

export default function App() {
  const [stackstate, setStackState] = useState(null);
  const [customPointOfSaleVisible, setCustomPointOfSaleVisible] = useState(false);
  if (!globals.session) return <div>Initializing session...</div>;

  //initialize stack
  if (!stackstate) {
      const dss = new DatonStackState()
      setStackState(dss);

      //uncomment to auto remove persistons from stack after saving
      //dss.onLayerSaved = datonKey => dss.removeByKey(datonKey, true);

      //sample custom lookup behavior
      dss.onCustomLookup = (editingLayer, editingTableDef, editingRow, editingColDef) => {
        if (editingColDef.name === 'customerId') return 'CustomerList|Company=Customer 1';
        return false;
      };

      return null;
  }

  const addToStack = (datonKey, asEmpty, businessContext) => {
    if (asEmpty)
      stackstate.addEmptyViewon(datonKey, businessContext);
    else
      stackstate.add(datonKey, datonKey.indexOf('|=-1') > 0, businessContext);
  };
  const pointOfSaleVisibilityToggle = () => {
    setCustomPointOfSaleVisible(!customPointOfSaleVisible);
  };

  //this is a sample whole-page layout for an app with the menu on the left and the daton stack in the main area
  return (
      <>
        <div className="main-left">
          <div style={{fontSize:'32pt', color:'#8b8', fontWeight: 'bold'}}>RetroDRY Sample App</div>
          <h3>Setup tables</h3>
          <button onClick={() => addToStack('PhoneTypeLookup|+')}>Phone Types</button>
          <button onClick={() => addToStack('SaleStatusLookup|+')}>Sale Statuses</button>
          <h3>Query</h3>
          <button onClick={() => addToStack('EmployeeList', false)}>Employees</button>
          <button onClick={() => addToStack('CustomerList', true)}>Customers</button>
          <button onClick={() => addToStack('CustomerList', true, 'POS')}>Customers (POS context)</button>
          <button onClick={() => addToStack('ItemList', true)}>Items</button>
          <button onClick={() => addToStack('SaleList', true)}>Sales</button>
          <button onClick={() => addToStack('SaleList|SaleDate=20200801~20200901', false)}>August Sales</button>
          <h3>Create new..</h3>
          <button onClick={() => addToStack('Employee|=-1')}>Employee</button>
          <button onClick={() => addToStack('Customer|=-1')}>Customer</button>
          <button onClick={() => addToStack('Item|=-1')}>Item</button>
          <button onClick={() => addToStack('Sale|=-1')}>Sale</button>
          <h3>Custom features</h3>
          <div>
            <input type="checkbox" onClick={pointOfSaleVisibilityToggle}/> Show custom point of sale inputs
          </div>
        </div>
        <div className="main-right">
          <DatonStack session={globals.session} stackstate={stackstate} />          
          {customPointOfSaleVisible && <PointOfSaleEntry session={globals.session} />}
        </div>
      </>
  );
}
