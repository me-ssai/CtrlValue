import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { PositionsComponent } from '../../positions/positions.component';
import { PriceHistoryComponent } from '../../price-history/price-history.component';
import { InstrumentsComponent } from '../../instruments/instruments.component';
import { EtfFormComponent } from './etf-form/etf-form.component';
import { PositionFormComponent } from '../../positions/position-form/position-form.component';

@Component({
    selector: 'app-etfs',
    standalone: true,
    imports: [
        CommonModule, MatTabsModule, MatIconModule, MatButtonModule, MatDialogModule,
        PositionsComponent, PriceHistoryComponent, InstrumentsComponent
    ],
    template: `
        <div class="page-container">
            <div class="page-header">
                <h1><mat-icon>analytics</mat-icon> ETFs & Funds</h1>
                <button mat-stroked-button (click)="addPosition()">
                    <mat-icon>add_chart</mat-icon>
                    Add Position
                </button>
                <button mat-raised-button color="primary" (click)="addEtf()">
                    <mat-icon>add</mat-icon>
                    Add ETF / Fund
                </button>
            </div>
            <mat-tab-group animationDuration="200ms">
                <mat-tab label="Portfolio">
                    <ng-template matTabContent>
                        <app-positions [instrumentTypeFilter]="etfTypes" [embedded]="true"></app-positions>
                    </ng-template>
                </mat-tab>
                <mat-tab label="Instruments">
                    <ng-template matTabContent>
                        <app-instruments [showHeader]="false" [typeFilter]="'ETF|FUND'"></app-instruments>
                    </ng-template>
                </mat-tab>
                <mat-tab label="Price History">
                    <ng-template matTabContent>
                        <app-price-history instrumentTypeFilter="ETF"></app-price-history>
                    </ng-template>
                </mat-tab>
            </mat-tab-group>
        </div>
    `
})
export class EtfsComponent {
    private dialog = inject(MatDialog);

    /** ETF and FUND types — used to filter positions in the Portfolio tab */
    readonly etfTypes = 'ETF|FUND';

    addEtf(): void {
        this.dialog.open(EtfFormComponent, {
            width: '620px',
            maxWidth: '95vw',
            data: {}
        });
    }

    addPosition(): void {
        this.dialog.open(PositionFormComponent, {
            width: '600px',
            maxWidth: '95vw',
            data: {}
        });
    }
}
