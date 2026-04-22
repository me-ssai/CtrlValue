import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FinanceService } from '../../services/finance.service';
import { Asset } from '../../models/api.models';
import { AssetFormComponent } from './asset-form/asset-form.component';

@Component({
    selector: 'app-assets',
    standalone: true,
    imports: [
        CommonModule,
        RouterModule,
        MatIconModule,
        MatButtonModule,
        MatCardModule,
        MatTooltipModule,
        MatSnackBarModule,
        MatDialogModule
    ],
    templateUrl: './assets.component.html',
    styleUrl: './assets.component.scss'
})
export class AssetsComponent implements OnInit {
    assets: Asset[] = [];
    loading = false;
    error: string | null = null;

    constructor(
        private financeService: FinanceService,
        private snackBar: MatSnackBar,
        private dialog: MatDialog
    ) { }

    ngOnInit(): void {
        this.loadAssets();
    }

    loadAssets(): void {
        this.loading = true;
        this.error = null;
        this.financeService.getAssets().subscribe({
            next: (data) => {
                this.assets = data;
                this.loading = false;
            },
            error: (err) => {
                this.error = 'Failed to load assets';
                this.loading = false;
                console.error(err);
            }
        });
    }

    getTotalValue(): number {
        return this.assets.reduce((sum, asset) => sum + asset.currentValue, 0);
    }

    createAsset(): void {
        const ref = this.dialog.open(AssetFormComponent, {
            width: '560px',
            maxWidth: '95vw',
            data: {}
        });
        ref.afterClosed().subscribe(result => {
            if (result) this.loadAssets();
        });
    }

    editAsset(id: string): void {
        const ref = this.dialog.open(AssetFormComponent, {
            width: '560px',
            maxWidth: '95vw',
            data: { assetId: id }
        });
        ref.afterClosed().subscribe(result => {
            if (result) this.loadAssets();
        });
    }

    deleteAsset(id: string, name: string): void {
        if (!confirm(`Are you sure you want to delete "${name}"?`)) return;
        this.financeService.deleteAsset(id).subscribe({
            next: () => {
                this.snackBar.open('Asset deleted', 'Close', { duration: 3000 });
                this.loadAssets();
            },
            error: (err) => {
                this.error = 'Failed to delete asset';
                console.error(err);
            }
        });
    }
}
