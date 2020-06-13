export function parseDatonKey(s: string): DatonKey {
    const parts = s.split('|');
    const typeName = parts.shift();
    if (!typeName) throw new Error('Invalid daton key');
    return new DatonKey(typeName, parts);
}

//A parsed daton key string
export default class DatonKey {
    //type name (the first pipe-delimited segment)
    typeName: string;
    
    //all other segments
    otherSegments: string[];

    constructor(typeName: string, otherSegments: string[]) {
        this.typeName = typeName;
        this.otherSegments = otherSegments;
    }

    //convert parsed segments to a daton key string
    toKeyString(): string {
        let ret = this.typeName;
        if (this.otherSegments && this.otherSegments.length) 
            ret += '|' + this.otherSegments.join();
        return ret;
    }

    //true if key refers to a new, unsaved persiston
    isNew(): boolean {
        return this.otherSegments.length === 1 && this.otherSegments[0] === '=-1';
    }

    //true if key refers to a persiston; false if refers to a viewon
    isPersiston(): boolean {
        if (this.otherSegments.length !== 1) return false;
        const seg1 = this.otherSegments[0];
        return seg1 === '+' || seg1[0] === '=';
    }

    persistonKeyAsString(): string {
        if (this.otherSegments.length != 1) return '';
        const seg = this.otherSegments[0];
        if (seg[0] !== '=') return ''; //malformed persiston key
        return seg.substr(1);
    }

    persistonKeyAsInt(): number {
        return parseInt(this.persistonKeyAsString(), 10);
    }
}