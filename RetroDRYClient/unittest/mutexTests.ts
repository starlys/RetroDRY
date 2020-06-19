import { ok } from 'assert';
import 'mocha';
import Mutex from '../src/mutex';

function wait(ms: number):Promise<void> { return new Promise(resolve => setTimeout(resolve, ms)); }

describe('mutex', () => {

    it('should not wait', async () => {
        const startedAt = new Date().getTime();
        const duration = new Date().getTime() - startedAt;
        ok(duration < 2); 
    });

    it('should wait', async () => {
        const startedAt = new Date().getTime();
        await wait(4);
        const duration = new Date().getTime() - startedAt;
        ok(duration >= 4 && duration < 8); 
    });

    it('should allow multiple executions of critical code', async () => {
        const startedAt = new Date().getTime();
        const criticalWork = async () => {
            await wait(10);
        }
        await Promise.all([criticalWork(), criticalWork(), criticalWork(), criticalWork(), criticalWork()]);
        const duration = new Date().getTime() - startedAt;
        ok(duration < 20); 
    });

    it('should prevent multiple executions of critical code', async () => {
        const mutex = new Mutex();

        const startedAt = new Date().getTime();
        const criticalWork = async () => {
            await mutex.acquire();
            await wait(10);
            mutex.release();
        }
        await Promise.all([criticalWork(), criticalWork(), criticalWork(), criticalWork(), criticalWork()]);
        const duration = new Date().getTime() - startedAt;
        ok(duration >= 50); 
    });

});