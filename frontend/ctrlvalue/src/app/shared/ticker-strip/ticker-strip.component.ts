import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule, DecimalPipe, CurrencyPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Subscription, interval } from 'rxjs';
import { switchMap, startWith } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface TickerItem {
    symbol: string;
    name: string;
    type: string;
    price: number | null;
    currency: string;
    changePercent: number | null;
    changeAmount: number | null;
    direction: 'UP' | 'DOWN' | 'FLAT';
    isUserHeld: boolean;
    userCurrentValue: number | null;
    userGainLoss: number | null;
    userGainLossPercent: number | null;
    asOfDate: string | null;
}

const HIDDEN_ROUTES = ['/login', '/register', '/verify-email', '/onboarding'];
const REFRESH_INTERVAL_MS = 5 * 60 * 1000;

@Component({
    selector: 'app-ticker-strip',
    standalone: true,
    imports: [CommonModule, DecimalPipe, CurrencyPipe],
    template: `
        <div class="ticker-strip" *ngIf="visible && displayItems.length > 0">
            <div class="ticker-track">
                <ng-container *ngFor="let item of displayItems">
                    <span class="ticker-item">
                        <span class="ticker-label">{{ item.symbol }}</span>
                        <span class="ticker-value" *ngIf="item.price !== null">
                            {{ item.price | currency:item.currency:'symbol':'1.2-2' }}
                        </span>
                        <span class="ticker-value" *ngIf="item.price === null">—</span>
                        <span class="ticker-delta"
                              [class.up]="item.direction === 'UP'"
                              [class.down]="item.direction === 'DOWN'"
                              *ngIf="item.changePercent !== null && item.direction !== 'FLAT'">
                            {{ item.direction === 'UP' ? '▲' : '▼' }}
                            {{ (item.changePercent < 0 ? -item.changePercent : item.changePercent) | number:'1.2-2' }}%
                        </span>
                    </span>
                    <span class="ticker-sep">|</span>
                </ng-container>
            </div>
        </div>
    `,
    styles: [`
        :host { display: block; }

        .ticker-strip {
            height: var(--ticker-height);
            background: var(--color-ticker-bg, var(--color-bg-secondary));
            border-bottom: 1px solid var(--color-border);
            overflow: hidden;
            display: flex;
            align-items: center;
            flex-shrink: 0;
        }

        .ticker-track {
            display: flex;
            align-items: center;
            gap: 0;
            white-space: nowrap;
            animation: ticker-scroll 50s linear infinite;
        }

        .ticker-track:hover {
            animation-play-state: paused;
        }

        @keyframes ticker-scroll {
            0%   { transform: translateX(0); }
            100% { transform: translateX(-50%); }
        }

        .ticker-item {
            display: inline-flex;
            align-items: center;
            gap: 0.35rem;
            padding: 0 1.25rem;
        }

        .ticker-label {
            font-family: var(--font-family-display);
            font-size: 0.625rem;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.12em;
            color: var(--color-sidebar-text-active);
        }

        .ticker-value {
            font-family: var(--font-family-mono);
            font-size: 0.75rem;
            font-weight: 400;
            color: var(--color-text-primary);
            letter-spacing: 0.02em;
        }

        .ticker-delta {
            font-family: var(--font-family-mono);
            font-size: 0.6875rem;
            font-weight: 500;
            letter-spacing: 0.02em;
        }

        .ticker-delta.up   { color: var(--color-accent-success); }
        .ticker-delta.down { color: var(--color-accent-danger); }

        .ticker-sep {
            color: var(--color-border);
            font-size: 0.875rem;
            line-height: 1;
            user-select: none;
        }
    `]
})
export class TickerStripComponent implements OnInit, OnDestroy {
    items: TickerItem[] = [];
    displayItems: TickerItem[] = [];
    visible = true;

    private sub?: Subscription;
    private readonly http = inject(HttpClient);
    private readonly router = inject(Router);

    ngOnInit(): void {
        this.visible = !HIDDEN_ROUTES.some(r => this.router.url.startsWith(r));
        if (!this.visible) return;

        this.sub = interval(REFRESH_INTERVAL_MS).pipe(
            startWith(0),
            switchMap(() => this.http.get<TickerItem[]>(`${environment.apiUrl}/ticker`))
        ).subscribe({
            next: items => {
                this.items = items;
                this.displayItems = [...items, ...items]; // doubled for seamless CSS loop
            },
            error: () => { /* ticker is non-critical */ }
        });
    }

    ngOnDestroy(): void {
        this.sub?.unsubscribe();
    }
}
