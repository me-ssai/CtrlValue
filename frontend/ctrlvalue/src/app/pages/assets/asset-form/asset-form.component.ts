import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FinanceService } from '../../../services/finance.service';
import { CreateAssetRequest, UpdateAssetRequest } from '../../../models/api.models';

export interface AssetDialogData {
    assetId?: string;
}

@Component({
    selector: 'app-asset-form',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatDialogModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatButtonModule,
        MatIconModule,
        MatSnackBarModule
    ],
    templateUrl: './asset-form.component.html',
    styleUrl: './asset-form.component.scss'
})
export class AssetFormComponent implements OnInit {
    private fb = inject(FormBuilder);
    private financeService = inject(FinanceService);
    private dialogRef = inject<MatDialogRef<AssetFormComponent>>(MatDialogRef);
    private snackBar = inject(MatSnackBar);
    data = inject<AssetDialogData>(MAT_DIALOG_DATA);

    assetForm: FormGroup;
    isEditMode = false;
    assetId: string | null = null;
    loading = false;
    categories = ['Liquid', 'SemiLiquid', 'NonLiquid', 'LongTerm'];

    constructor() {
        this.assetForm = this.fb.group({
            name: ['', [Validators.required, Validators.maxLength(256)]],
            category: ['Liquid', Validators.required],
            currentValue: [0, [Validators.required, Validators.min(0)]],
            currency: ['AUD', Validators.required],
            description: ['']
        });
    }

    ngOnInit(): void {
        this.assetId = this.data?.assetId || null;
        this.isEditMode = !!this.assetId;
        if (this.isEditMode && this.assetId) {
            this.loadAsset(this.assetId);
        }
    }

    loadAsset(id: string): void {
        this.loading = true;
        this.financeService.getAsset(id).subscribe({
            next: (asset) => {
                this.assetForm.patchValue({
                    name: asset.name,
                    category: asset.category,
                    currentValue: asset.currentValue,
                    currency: asset.currency,
                    description: asset.description || ''
                });
                this.loading = false;
            },
            error: (err) => {
                this.loading = false;
                console.error(err);
                this.snackBar.open('Failed to load asset', 'Close', { duration: 5000 });
                this.dialogRef.close(false);
            }
        });
    }

    onSubmit(): void {
        if (this.assetForm.invalid) {
            this.assetForm.markAllAsTouched();
            return;
        }
        this.loading = true;
        const formValue = this.assetForm.value;

        if (this.isEditMode && this.assetId) {
            const request: UpdateAssetRequest = formValue;
            this.financeService.updateAsset(this.assetId, request).subscribe({
                next: () => {
                    this.snackBar.open('Asset updated successfully', 'Close', { duration: 3000 });
                    this.dialogRef.close(true);
                },
                error: (err) => {
                    this.loading = false;
                    console.error(err);
                    this.snackBar.open('Failed to update asset', 'Close', { duration: 5000 });
                }
            });
        } else {
            const request: CreateAssetRequest = formValue;
            this.financeService.createAsset(request).subscribe({
                next: () => {
                    this.snackBar.open('Asset created successfully', 'Close', { duration: 3000 });
                    this.dialogRef.close(true);
                },
                error: (err) => {
                    this.loading = false;
                    console.error(err);
                    this.snackBar.open('Failed to create asset', 'Close', { duration: 5000 });
                }
            });
        }
    }

    cancel(): void {
        this.dialogRef.close(false);
    }
}
