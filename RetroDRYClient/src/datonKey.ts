//A parsed daton key string
export default class DatonKey {
    //type name (the first pipe-delimited segment)
    typeName: string;
    
    //all other segments
    otherSegments: string[];

    static parse(s: string): DatonKey {
        const parts = s.split('|');
        const typeName = parts.shift();
        if (!typeName) throw new Error('Invalid daton key');
        return new DatonKey(typeName, parts);
    }

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
}