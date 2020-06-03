import React from 'react';
import { Session } from 'retrodry';

export default function() {
    let ses = new Session();  
    return <div>Hey, {ses.longPollDelay}</div>
}