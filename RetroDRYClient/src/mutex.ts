//mutex to ensure async code is one-at-a-time
//To use, await acquire() before the critical code, then call release after the critical code
export default class Mutex {
    private locked: boolean = false;
    private waiters: Function[] = [];

    acquire(): Promise<boolean> {
        if (!this.locked) {
            this.locked = true;
            return Promise.resolve(true);
        } 
        const p = new Promise<boolean>(resolve => {
            this.waiters.push(resolve);
        });
        return p;
    }

    release() {
        if (!this.waiters.length) {
            this.locked = false;
        } else {
            const resolve = this.waiters.shift();
            resolve!(true);
        }
    }
}