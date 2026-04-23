import { Injectable, NgZone, OnDestroy, inject } from '@angular/core';
import { Subject, Subscription, fromEvent, merge } from 'rxjs';

const IDLE_TIMEOUT_MS  = 30 * 60 * 1000;   // 30 minutes
const WARNING_BEFORE_MS = 5 * 60 * 1000;   // warn 5 min before timeout

@Injectable({ providedIn: 'root' })
export class IdleTimeoutService implements OnDestroy {
    private zone = inject(NgZone);

    private idleTimer: ReturnType<typeof setTimeout> | null = null;
    private warnTimer: ReturnType<typeof setTimeout> | null = null;
    private activitySub: Subscription | null = null;

    readonly warn$    = new Subject<void>();   // emits when 5-min warning should appear
    readonly timeout$ = new Subject<void>();

    start(): void {
        const activity$ = merge(
            fromEvent(document, 'mousemove'),
            fromEvent(document, 'keydown'),
            fromEvent(document, 'click'),
            fromEvent(document, 'touchstart')
        );

        this.activitySub = activity$.subscribe(() => this.reset());
        this.scheduleTimers();
    }

    stop(): void {
        this.clearTimers();
        this.activitySub?.unsubscribe();
        this.activitySub = null;
    }

    reset(): void {
        this.clearTimers();
        this.scheduleTimers();
    }

    private scheduleTimers(): void {
        this.zone.runOutsideAngular(() => {
            this.warnTimer = setTimeout(() => {
                this.zone.run(() => this.warn$.next());
            }, IDLE_TIMEOUT_MS - WARNING_BEFORE_MS);

            this.idleTimer = setTimeout(() => {
                this.zone.run(() => this.timeout$.next());
            }, IDLE_TIMEOUT_MS);
        });
    }

    private clearTimers(): void {
        if (this.warnTimer)  { clearTimeout(this.warnTimer);  this.warnTimer  = null; }
        if (this.idleTimer)  { clearTimeout(this.idleTimer);  this.idleTimer  = null; }
    }

    ngOnDestroy(): void { this.stop(); }
}
