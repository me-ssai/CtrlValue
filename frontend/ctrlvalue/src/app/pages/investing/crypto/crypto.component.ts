import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { PositionsComponent } from '../../positions/positions.component';
import { PriceHistoryComponent } from '../../price-history/price-history.component';
import { InstrumentsComponent } from '../../instruments/instruments.component';
import { InstrumentFormComponent } from '../../instruments/instrument-form/instrument-form.component';

@Component({
    selector: 'app-crypto',
    standalone: true,
    imports: [
        CommonModule, MatTabsModule, MatIconModule, MatButtonModule, MatDialogModule,
        PositionsComponent, PriceHistoryComponent, InstrumentsComponent
    ],
    template: `
        <div class="page-container">
            <div class="page-header">
                <h1><mat-icon>currency_bitcoin</mat-icon> Crypto</h1>
                <button mat-raised-button color="primary" (click)="addCoin()">
                    <mat-icon>add</mat-icon>
                    Add Coin
                </button>
            </div>
            <mat-tab-group animationDuration="200ms">
                <mat-tab label="Portfolio">
                    <ng-template matTabContent>
                        <app-positions instrumentTypeFilter="CRYPTO"></app-positions>
                    </ng-template>
                </mat-tab>
                <mat-tab label="Instruments">
                    <ng-template matTabContent>
                        <app-instruments [showHeader]="false" [typeFilter]="'CRYPTO'"></app-instruments>
                    </ng-template>
                </mat-tab>
                <mat-tab label="Price History">
                    <ng-template matTabContent>
                        <app-price-history instrumentTypeFilter="CRYPTO"></app-price-history>
                    </ng-template>
                </mat-tab>
            </mat-tab-group>
        </div>
    `
})
export class CryptoComponent {
    constructor(private dialog: MatDialog) {}

    addCoin(): void {
        this.dialog.open(InstrumentFormComponent, {
            width: '560px',
            maxWidth: '95vw',
            data: { defaultType: 'CRYPTO', defaultPriceProvider: 'COINGECKO' }
        });
    }
}
