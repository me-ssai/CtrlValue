import { Component, OnInit, Inject } from '@angular/core';
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
import { CreateCategoryRequest, Category } from '../../../models/api.models';

export interface CategoryDialogData {
    // create-only for now; extend later for edit
}

@Component({
    selector: 'app-category-form',
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
    templateUrl: './category-form.component.html',
    styleUrl: './category-form.component.scss'
})
export class CategoryFormComponent implements OnInit {
    categoryForm: FormGroup;
    loading = false;
    parentCategories: Category[] = [];
    categoryTypes = ['INCOME', 'EXPENSE', 'TRANSFER'];

    constructor(
        private fb: FormBuilder,
        private financeService: FinanceService,
        private dialogRef: MatDialogRef<CategoryFormComponent>,
        private snackBar: MatSnackBar,
        @Inject(MAT_DIALOG_DATA) public data: CategoryDialogData
    ) {
        this.categoryForm = this.fb.group({
            name: ['', [Validators.required, Validators.maxLength(100)]],
            categoryType: ['EXPENSE', Validators.required],
            parentCategoryId: [''],
            color: [''],
            icon: ['']
        });
    }

    ngOnInit(): void {
        this.loadParentCategories();
    }

    loadParentCategories(): void {
        this.financeService.getCategories().subscribe({
            next: (data) => { this.parentCategories = data; },
            error: (err) => { console.error('Failed to load parent categories', err); }
        });
    }

    onSubmit(): void {
        if (this.categoryForm.invalid) {
            this.categoryForm.markAllAsTouched();
            return;
        }
        this.loading = true;
        const formValue = this.categoryForm.value;
        const request: CreateCategoryRequest = {
            name: formValue.name,
            categoryType: formValue.categoryType,
            parentCategoryId: formValue.parentCategoryId || undefined,
            color: formValue.color || undefined,
            icon: formValue.icon || undefined
        };

        this.financeService.createCategory(request).subscribe({
            next: () => {
                this.snackBar.open('Category created successfully', 'Close', { duration: 3000 });
                this.dialogRef.close(true);
            },
            error: (err) => {
                this.loading = false;
                console.error(err);
                this.snackBar.open(err.error?.message || 'Failed to create category', 'Close', { duration: 5000 });
            }
        });
    }

    cancel(): void {
        this.dialogRef.close(false);
    }
}
