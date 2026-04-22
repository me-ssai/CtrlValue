import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FinanceService } from '../../../services/finance.service';
import { CreateLiabilityRequest, UpdateLiabilityRequest } from '../../../models/api.models';

export interface LiabilityDialogData {
    liabilityId?: string;
}

@Component({
    selector: 'app-liability-form',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatDialogModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
        MatSnackBarModule
    ],
    templateUrl: './liability-form.component.html',
    styleUrl: './liability-form.component.scss'
})
export class LiabilityFormComponent implements OnInit {
    liabilityForm: FormGroup;
    isEditMode = false;
    liabilityId: string | null = null;
    loading = false;

    constructor(
        private fb: FormBuilder,
        private financeService: FinanceService,
        private dialogRef: MatDialogRef<LiabilityFormComponent>,
        private snackBar: MatSnackBar,
        @Inject(MAT_DIALOG_DATA) public data: LiabilityDialogData
    ) {
        this.liabilityForm = this.fb.group({
            name: ['', [Validators.required, Validators.maxLength(256)]],
            category: ['', Validators.required],
            outstandingAmount: [0, [Validators.required, Validators.min(0)]],
            interestRate: [null, [Validators.min(0), Validators.max(100)]],
            currency: ['AUD', Validators.required],
            description: ['']
        });
    }

    ngOnInit(): void {
        this.liabilityId = this.data?.liabilityId || null;
        this.isEditMode = !!this.liabilityId;
        if (this.isEditMode && this.liabilityId) {
            this.loadLiability(this.liabilityId);
        }
    }

    loadLiability(id: string): void {
        this.loading = true;
        this.financeService.getLiability(id).subscribe({
            next: (liability) => {
                this.liabilityForm.patchValue(liability);
                this.loading = false;
            },
            error: (err) => {
                this.loading = false;
                console.error(err);
                this.snackBar.open('Failed to load liability', 'Close', { duration: 5000 });
                this.dialogRef.close(false);
            }
        });
    }

    onSubmit(): void {
        if (this.liabilityForm.invalid) {
            this.liabilityForm.markAllAsTouched();
            return;
        }
        this.loading = true;
        const formValue = this.liabilityForm.value;

        if (this.isEditMode && this.liabilityId) {
            const request: UpdateLiabilityRequest = formValue;
            this.financeService.updateLiability(this.liabilityId, request).subscribe({
                next: () => {
                    this.snackBar.open('Liability updated successfully', 'Close', { duration: 3000 });
                    this.dialogRef.close(true);
                },
                error: (err) => {
                    this.loading = false;
                    console.error(err);
                    this.snackBar.open('Failed to update liability', 'Close', { duration: 5000 });
                }
            });
        } else {
            const request: CreateLiabilityRequest = formValue;
            this.financeService.createLiability(request).subscribe({
                next: () => {
                    this.snackBar.open('Liability created successfully', 'Close', { duration: 3000 });
                    this.dialogRef.close(true);
                },
                error: (err) => {
                    this.loading = false;
                    console.error(err);
                    this.snackBar.open('Failed to create liability', 'Close', { duration: 5000 });
                }
            });
        }
    }

    cancel(): void {
        this.dialogRef.close(false);
    }
}
