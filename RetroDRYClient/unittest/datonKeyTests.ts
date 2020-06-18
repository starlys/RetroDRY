import { equal } from 'assert';
import 'mocha';
import DatonKey, {parseDatonKey} from '../src/datonKey';

describe('daton key', () => {

    it('should parse', () => {
        //new persiston
        let parsedKey = parseDatonKey('Customer|=-1');
        equal(parsedKey.isNew(), true);
        equal(parsedKey.isPersiston(), true);
        equal(parsedKey.typeName, 'Customer');
        equal(parsedKey.otherSegments.length, 1);
        equal(parsedKey.otherSegments[0], '=-1');

        //existing persiston
        parsedKey = parseDatonKey('Customer|=67');
        equal(parsedKey.isNew(), false);
        equal(parsedKey.isPersiston(), true);
        equal(parsedKey.typeName, 'Customer');
        equal(parsedKey.otherSegments.length, 1);
        equal(parsedKey.otherSegments[0], '=67');
        equal(parsedKey.persistonKeyAsInt(), 67);
        equal(parsedKey.persistonKeyAsString(), '67');

        //whole table persiston
        parsedKey = parseDatonKey('CustomerType|+');
        equal(parsedKey.isNew(), false);
        equal(parsedKey.isPersiston(), true);
        equal(parsedKey.typeName, 'CustomerType');
        equal(parsedKey.otherSegments.length, 1);
        equal(parsedKey.otherSegments[0], '+');

        //viewon without criteria
        parsedKey = parseDatonKey('CustomerList');
        equal(parsedKey.isNew(), false);
        equal(parsedKey.isPersiston(), false);
        equal(parsedKey.typeName, 'CustomerList');
        equal(parsedKey.otherSegments.length, 0);

        //viewon with criteria
        parsedKey = parseDatonKey('CustomerList|code1=\\\\/|code2=\\||code3=a=\\|b\\|');
        equal(parsedKey.isNew(), false);
        equal(parsedKey.isPersiston(), false);
        equal(parsedKey.typeName, 'CustomerList');
        equal(parsedKey.otherSegments.length, 3);
        equal(parsedKey.otherSegments[0], 'code1=\\/');
        equal(parsedKey.otherSegments[1], 'code2=|');
        equal(parsedKey.otherSegments[2], 'code3=a=|b|');
    });

    it('should escape segment in isolation', () => {
        let k = new DatonKey('ItemList', []);
        equal(k.escapeSegment('x'), 'x');
    });

    it('should escape segments', () => {
        let k = new DatonKey('CustomerList', ['code1=\\/', 'code3=a=|b|', 'code2=|', '_sort=firstName', '_page=3']);
        equal(k.toKeyString(), 'CustomerList|_page=3|_sort=firstName|code1=\\\\/|code2=\\||code3=a=\\|b\\|');
    });
});