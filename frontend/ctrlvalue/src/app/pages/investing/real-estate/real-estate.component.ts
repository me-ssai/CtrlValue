import { Component, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, TitleCasePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PropertyService, Property } from '../../../services/property.service';
import { PropertyFormComponent } from './property-form/property-form.component';

@Component({
    selector: 'app-real-estate',
    standalone: true,
    imports: [
        CommonModule, RouterModule, MatTableModule, MatIconModule, MatButtonModule,
        MatChipsModule, MatTooltipModule, MatDialogModule, CurrencyPipe, TitleCasePipe
    ],
    templateUrl: './real-estate.component.html'
})
export class RealEstateComponent implements OnInit {
    displayedColumns = ['address', 'type', 'specs', 'purchasePrice', 'currentValue', 'gain', 'actions'];
    dataSource = new MatTableDataSource<Property>();

    constructor(
        private propertyService: PropertyService,
        private dialog: MatDialog,
        private snackBar: MatSnackBar
    ) {}

    ngOnInit(): void { this.loadProperties(); }

    loadProperties(): void {
        this.propertyService.getProperties().subscribe({
            next: (props) => { this.dataSource.data = props; },
            error: () => this.snackBar.open('Failed to load properties', 'Close', { duration: 3000 })
        });
    }

    gain(p: Property): number { return p.currentValue - p.purchasePrice; }
    gainPercent(p: Property): number { return p.purchasePrice > 0 ? (this.gain(p) / p.purchasePrice) * 100 : 0; }

    addProperty(): void {
        this.dialog.open(PropertyFormComponent, { width: '640px', maxWidth: '95vw', data: {} })
            .afterClosed().subscribe(r => { if (r) this.loadProperties(); });
    }

    editProperty(id: string): void {
        this.dialog.open(PropertyFormComponent, { width: '640px', maxWidth: '95vw', data: { propertyId: id } })
            .afterClosed().subscribe(r => { if (r) this.loadProperties(); });
    }

    deleteProperty(id: string): void {
        if (!confirm('Delete this property? The linked account will also be removed.')) return;
        this.propertyService.deleteProperty(id).subscribe({
            next: () => { this.snackBar.open('Property deleted', 'Close', { duration: 3000 }); this.loadProperties(); },
            error: () => this.snackBar.open('Failed to delete property', 'Close', { duration: 3000 })
        });
    }
}
