import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { InstrumentsComponent } from '../../instruments/instruments.component';
import { PositionsComponent } from '../../positions/positions.component';
import { PriceHistoryComponent } from '../../price-history/price-history.component';
import { InstrumentFormComponent } from '../../instruments/instrument-form/instrument-form.component';
import { PositionFormComponent } from '../../positions/position-form/position-form.component';

@Component({
    selector: 'app-stocks',
    standalone: true,
    imports: [CommonModule, MatTabsModule, MatIconModule, MatButtonModule, MatDialogModule,
              InstrumentsComponent, PositionsComponent, PriceHistoryComponent],
    template: `
        <div class="page-container">
            <div class="page-header">
                <h1><mat-icon>candlestick_chart</mat-icon> Stocks</h1>
                <button mat-stroked-button (click)="addPosition()">
                    <mat-icon>add_chart</mat-icon>
                    Add Position
                </button>
                <button mat-raised-button color="primary" (click)="addInstrument()">
                    <mat-icon>add</mat-icon>
                    New Instrument
                </button>
            </div>
            <mat-tab-group animationDuration="200ms">
                <mat-tab label="Instruments">
                    <ng-template matTabContent>
                        <app-instruments [showHeader]="false" [typeFilter]="'STOCK'"></app-instruments>
                    </ng-template>
                </mat-tab>
                <mat-tab label="Positions">
                    <ng-template matTabContent>
                        <app-positions instrumentTypeFilter="STOCK" [embedded]="true"></app-positions>
                    </ng-template>
                </mat-tab>
                <mat-tab label="Price History">
                    <ng-template matTabContent>
                        <app-price-history instrumentTypeFilter="STOCK"></app-price-history>
                    </ng-template>
                </mat-tab>
            </mat-tab-group>
        </div>
    `
})
export class StocksComponent {
    constructor(private dialog: MatDialog) {}

    addInstrument(): void {
        this.dialog.open(InstrumentFormComponent, { width: '560px', maxWidth: '95vw', data: {} });
    }

    addPosition(): void {
        this.dialog.open(PositionFormComponent, { width: '600px', maxWidth: '95vw', data: {} });
    }
}
