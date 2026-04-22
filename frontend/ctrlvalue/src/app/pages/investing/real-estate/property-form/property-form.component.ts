import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDividerModule } from '@angular/material/divider';
import { PropertyService } from '../../../../services/property.service';

export interface PropertyDialogData {
    propertyId?: string;
}

const PROPERTY_TYPES = ['RESIDENTIAL', 'COMMERCIAL', 'INDUSTRIAL', 'LAND', 'RURAL'];

@Component({
    selector: 'app-property-form',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule,
        MatSelectModule, MatButtonModule, MatIconModule, MatCheckboxModule,
        MatDatepickerModule, MatNativeDateModule, MatDividerModule
    ],
    templateUrl: './property-form.component.html'
})
export class PropertyFormComponent implements OnInit {
    form: FormGroup;
    isEditMode = false;
    propertyId: string | null = null;
    loading = false;
    propertyTypes = PROPERTY_TYPES;

    constructor(
        private fb: FormBuilder,
        private propertyService: PropertyService,
        private dialogRef: MatDialogRef<PropertyFormComponent>,
        private snackBar: MatSnackBar,
        @Inject(MAT_DIALOG_DATA) public data: PropertyDialogData
    ) {
        this.form = this.fb.group({
            // Address
            address: ['', Validators.required],
            suburb: [''],
            state: [''],
            postCode: [''],
            country: ['AUS', Validators.required],
            // Classification
            propertyType: ['RESIDENTIAL', Validators.required],
            // Physical specs
            bedrooms: [null],
            bathrooms: [null],
            carSpaces: [null],
            landSizeSqm: [null],
            floorSizeSqm: [null],
            yearBuilt: [null],
            // Financials
            purchasePrice: [null, [Validators.required, Validators.min(0)]],
            purchaseDate: [new Date(), Validators.required],
            currency: ['AUD', Validators.required],
            // Rental
            isRental: [false],
            weeklyRentTarget: [null]
        });
    }

    ngOnInit(): void {
        this.propertyId = this.data?.propertyId ?? null;
        this.isEditMode = !!this.propertyId;
        if (this.isEditMode && this.propertyId) {
            this.loading = true;
            this.propertyService.getPropertyById(this.propertyId).subscribe({
                next: (p) => {
                    this.form.patchValue({ ...p, purchaseDate: new Date(p.purchaseDate) });
                    this.loading = false;
                },
                error: () => {
                    this.snackBar.open('Failed to load property', 'Close', { duration: 3000 });
                    this.dialogRef.close(false);
                }
            });
        }
    }

    onSubmit(): void {
        if (this.form.invalid) { this.form.markAllAsTouched(); return; }
        this.loading = true;
        const v = this.form.value;

        if (this.isEditMode && this.propertyId) {
            this.propertyService.updateProperty(this.propertyId, {
                address: v.address, suburb: v.suburb || undefined, state: v.state || undefined,
                postCode: v.postCode || undefined, country: v.country, propertyType: v.propertyType,
                bedrooms: v.bedrooms || undefined, bathrooms: v.bathrooms || undefined,
                carSpaces: v.carSpaces || undefined, landSizeSqm: v.landSizeSqm || undefined,
                floorSizeSqm: v.floorSizeSqm || undefined, yearBuilt: v.yearBuilt || undefined,
                isRental: v.isRental, weeklyRentTarget: v.weeklyRentTarget || undefined
            }).subscribe({
                next: () => { this.snackBar.open('Property updated', 'Close', { duration: 3000 }); this.dialogRef.close(true); },
                error: (e) => { this.snackBar.open(e.error?.message || 'Failed to update', 'Close', { duration: 3000 }); this.loading = false; }
            });
        } else {
            this.propertyService.createProperty({
                address: v.address, suburb: v.suburb || undefined, state: v.state || undefined,
                postCode: v.postCode || undefined, country: v.country, propertyType: v.propertyType,
                bedrooms: v.bedrooms || undefined, bathrooms: v.bathrooms || undefined,
                carSpaces: v.carSpaces || undefined, landSizeSqm: v.landSizeSqm || undefined,
                floorSizeSqm: v.floorSizeSqm || undefined, yearBuilt: v.yearBuilt || undefined,
                purchasePrice: v.purchasePrice, purchaseDate: v.purchaseDate instanceof Date ? v.purchaseDate.toISOString() : v.purchaseDate,
                isRental: v.isRental, weeklyRentTarget: v.weeklyRentTarget || undefined,
                entityId: '', // resolved server-side from auth context
                currency: v.currency
            }).subscribe({
                next: () => { this.snackBar.open('Property created', 'Close', { duration: 3000 }); this.dialogRef.close(true); },
                error: (e) => { this.snackBar.open(e.error?.message || 'Failed to create', 'Close', { duration: 3000 }); this.loading = false; }
            });
        }
    }

    cancel(): void { this.dialogRef.close(false); }
}
