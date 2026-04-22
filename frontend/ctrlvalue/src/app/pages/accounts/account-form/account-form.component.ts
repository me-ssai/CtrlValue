import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatRadioModule } from '@angular/material/radio';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { FinanceService } from '../../../services/finance.service';
import { CreateAccountRequest } from '../../../models/api.models';
import { HelpIconComponent } from '../../../shared/help-icon/help-icon.component';
import { filterInstitutions, getInstitutionList } from '../../../shared/institution-suggestions';

@Component({
    selector: 'app-account-form',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        RouterModule,
        MatDialogModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatButtonModule,
        MatIconModule,
        MatDatepickerModule,
        MatNativeDateModule,
        MatSnackBarModule,
        MatCheckboxModule,
        MatExpansionModule,
        MatRadioModule,
        MatAutocompleteModule,
        HelpIconComponent
    ],
    templateUrl: './account-form.component.html',
    styleUrl: './account-form.component.scss'
})
export class AccountFormComponent {
    accountForm: FormGroup;
    loading = false;

    accountTypes = ['ASSET', 'LIABILITY'];
    assetClasses = ['CASH', 'STOCK', 'ETF', 'METAL', 'VEHICLE', 'PROPERTY', 'SUPER', 'BUSINESS', 'CRYPTO', 'OTHER'];
    liquidityClasses = ['LIQUID', 'SEMI_LIQUID', 'ILLIQUID', 'LOCKED'];

    filteredInstitutions: string[] = [];

    constructor(
        private fb: FormBuilder,
        private financeService: FinanceService,
        private dialogRef: MatDialogRef<AccountFormComponent>,
        private snackBar: MatSnackBar
    ) {
        this.accountForm = this.fb.group({
            name: ['', [Validators.required, Validators.maxLength(256)]],
            accountType: ['ASSET', Validators.required],
            assetClass: [''],
            liquidityClass: [''],
            currency: ['AUD', Validators.required],
            institution: [''],
            accountNumber: [''],
            notes: [''],
            creditLimit: [null],
            startingBalance: [0],
            startingBalanceDate: [new Date()],
            isOffsetAccount: [false],
            openedAt: [null],
            closedAt: [null],
            externalId: ['']
        });

        const updateInstitutions = () => {
            const type = this.accountForm.get('accountType')!.value;
            const cls  = this.accountForm.get('assetClass')!.value;
            const term = this.accountForm.get('institution')!.value;
            this.filteredInstitutions = filterInstitutions(getInstitutionList(type, cls), term);
        };

        this.accountForm.get('accountType')!.valueChanges.subscribe(() => updateInstitutions());
        this.accountForm.get('assetClass')!.valueChanges.subscribe(() => updateInstitutions());
        this.accountForm.get('institution')!.valueChanges.subscribe(() => updateInstitutions());
        updateInstitutions();
    }

    onSubmit(): void {
        if (this.accountForm.invalid) {
            this.accountForm.markAllAsTouched();
            return;
        }

        this.loading = true;

        const formValue = this.accountForm.value;
        const request: CreateAccountRequest = {
            name: formValue.name,
            accountType: formValue.accountType,
            assetClass: formValue.assetClass || undefined,
            liquidityClass: formValue.liquidityClass || undefined,
            currency: formValue.currency,
            institution: formValue.institution || undefined,
            accountNumber: formValue.accountNumber || undefined,
            notes: formValue.notes || undefined,
            creditLimit: formValue.creditLimit || undefined,
            startingBalance: formValue.startingBalance ?? 0,
            startingBalanceDate: formValue.startingBalanceDate ?? new Date(),
            isOffsetAccount: formValue.isOffsetAccount ?? false,
            openedAt: formValue.openedAt || undefined,
            closedAt: formValue.closedAt || undefined,
            externalId: formValue.externalId || undefined
        };

        this.financeService.createAccount(request).subscribe({
            next: () => {
                this.snackBar.open('Account created successfully', 'Close', { duration: 3000 });
                this.dialogRef.close(true);
            },
            error: (err) => {
                this.loading = false;
                console.error(err);
                this.snackBar.open(err.error?.message || 'Failed to create account', 'Close', { duration: 5000 });
            }
        });
    }

    cancel(): void {
        this.dialogRef.close(false);
    }
}
