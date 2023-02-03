//parse a daton key string (performs unescaping)
export function parseDatonKey(s: string): DatonKey {
    const parts = parseSegments(s);
    const typeName = parts.shift();
    if (!typeName) throw new Error('Invalid daton key');

    return new DatonKey(typeName, parts);
}

function parseSegments(s: string) {
    let segments = s.split('|');

    //unescape; and
    //in case any of the segments contained \|, it would be misinterpreted as 2 segments, so fix that
    for (let i = segments.length - 1; i >= 0; --i) {
        let segi = segments[i].replace(/\\\\/g, '\x01');
        if (segi[segi.length - 1] === '\\')
        {
            segi = segi.substring(0, segi.length - 1) + '|' + segments[i + 1];
            segments.splice(i + 1, 1);
        }
        segments[i] = segi.replace(/\x01/g, '\\');
    }
    return segments;
}

function caseInsensitiveComparer(a:any, b:any) {
    return a.localeCompare(b, undefined, {sensitivity: 'base'});
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
        if (this.otherSegments && this.otherSegments.length) {
            this.otherSegments.sort(caseInsensitiveComparer);
            ret += '|' + this.otherSegments.map(this.escapeSegment).join('|');
        }
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
        return seg.substring(1);
    }

    persistonKeyAsInt(): number {
        return parseInt(this.persistonKeyAsString(), 10);
    }

    escapeSegment(s: string): string {
        return s.replace(/\\/g, '\\\\').replace(/\|/g, '\\|');
    }
}