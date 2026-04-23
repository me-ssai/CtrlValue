import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { PositionsComponent } from '../../positions/positions.component';
import { PriceHistoryComponent } from '../../price-history/price-history.component';
import { InstrumentsComponent } from '../../instruments/instruments.component';
import { BondFormComponent } from './bond-form/bond-form.component';
import { PositionFormComponent } from '../../positions/position-form/position-form.component';

@Component({
    selector: 'app-bonds',
    standalone: true,
    imports: [
        CommonModule, MatTabsModule, MatIconModule, MatButtonModule, MatDialogModule,
        PositionsComponent, PriceHistoryComponent, InstrumentsComponent
    ],
    template: `
        <div class="page-container">
            <div class="page-header">
                <h1><mat-icon>receipt_long</mat-icon> Bonds & Fixed Income</h1>
                <button mat-stroked-button (click)="addPosition()">
                    <mat-icon>add_chart</mat-icon>
                    Add Position
                </button>
                <button mat-raised-button color="primary" (click)="addBond()">
                    <mat-icon>add</mat-icon>
                    Add Bond
                </button>
            </div>
            <mat-tab-group animationDuration="200ms">
                <mat-tab label="Portfolio">
                    <ng-template matTabContent>
                        <app-positions instrumentTypeFilter="BOND" [embedded]="true"></app-positions>
                    </ng-template>
                </mat-tab>
                <mat-tab label="Instruments">
                    <ng-template matTabContent>
                        <app-instruments [showHeader]="false" [typeFilter]="'BOND'"></app-instruments>
                    </ng-template>
                </mat-tab>
                <mat-tab label="Price History">
                    <ng-template matTabContent>
                        <app-price-history instrumentTypeFilter="BOND"></app-price-history>
                    </ng-template>
                </mat-tab>
            </mat-tab-group>
        </div>
    `
})
export class BondsComponent {
    private dialog = inject(MatDialog);


    addBond(): void {
        this.dialog.open(BondFormComponent, {
            width: '640px',
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
