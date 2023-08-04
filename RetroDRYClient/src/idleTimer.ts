//idle timer utility
export default class IdleTimer {
    private static lastActivityMillis: number = 0;
    private static initialized: boolean = false;

    private intervalId: number = 0; //0 means this instance is not active
    private warningWasGiven = false; 

    //start timing user inactivity; if it exceeds the given number of seconds (secondsToWarning), call the warning callback;
    //then if there is no activity from that point for more time (secondsToTimeout, measured from the beginning), call the timeout callback.
    //If the warning was called then there was user activity before the final callback, calls the rescind callback.
    start(secondsToWarning: number, secondsToTimeout: number, warningCallback: Function, 
        rescindWarningCallback: Function, timeoutCallback: Function): void {

        //static initialization
        if (!IdleTimer.initialized) {
            IdleTimer.initialized = true;

            //this listener never gets unhooked!
            IdleTimer.lastActivityMillis = Date.now();
            document.addEventListener('keydown', () => IdleTimer.lastActivityMillis = Date.now());
            document.addEventListener('mousedown', () => IdleTimer.lastActivityMillis = Date.now());
        }

        this.stop();
        this.intervalId = window.setInterval(() => {
            if (!this.intervalId) return;
            if (!this.warningWasGiven) {
                //give warning
                if (this.isExceeded(secondsToWarning)) {
                    this.warningWasGiven = true;
                    if (warningCallback) warningCallback();
                }
            } else { //warning already given
                //final timeout
                if (this.isExceeded(secondsToTimeout)) {
                    this.stop();
                    timeoutCallback();
                }
                //rescind warning
                else if (!this.isExceeded(secondsToWarning)) {
                    this.warningWasGiven = false;
                    if (rescindWarningCallback) rescindWarningCallback();
                }
            }
        }, 2000);
    }

    //stop timing user inactivity
    stop(): void {
        if (this.intervalId) window.clearInterval(this.intervalId);
        this.intervalId = 0;
    }

    //true if the current time has exceeded the given number of seconds past the last user activity
    private isExceeded(seconds: number) {
        return Date.now() > IdleTimer.lastActivityMillis + seconds * 1000;
    }
}