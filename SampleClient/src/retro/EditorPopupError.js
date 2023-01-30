import React from 'react';

//shows a popup error or nothing under the point in the DOM where it is included
//props.message is the message to show
//props.show is true to show it
const Component = (props) => {
    if (!props.message || !props.show) return null;
    return <span className="popup-error">{props.message}</span>;
};

export default Component;
