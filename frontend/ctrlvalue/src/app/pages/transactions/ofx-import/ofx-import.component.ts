import { Component, OnInit, inject } from '@angular/core';

import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';

// Angular Material
import { MatStepperModule } from '@angular/material/stepper';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatDialogRef, MatDialogModule, MatDialog, MAT_DIALOG_DATA } from '@angular/material/dialog';

import { MatCheckboxModule } from '@angular/material/checkbox';

// Services / DTOs
import {
    OfxImportService,
    OfxImportedTransactionsFileDto,
    OfxStagedImportReviewDto,
    OfxStagingRowDto
} from '../../../services/ofx-import.service';
import { UpdateStagingRowRequest } from '../../../services/qif-import.service';
import { FinanceService } from '../../../services/finance.service';
import { AccountKeywordRuleService } from '../../../services/account-keyword-rule.service';
import { AccountDto } from '../../../services/api.generated';
import { KeywordMatchType, CreateAccountKeywordRuleRequest } from '../../../models/api.models';

// ── Inline confirm dialog ────────────────────────────────────────────────────

import { Component as NgComponent } from '@angular/core';

@NgComponent({
    selector: 'app-ofx-cancel-confirm-dialog',
    standalone: true,
    imports: [MatButtonModule, MatDialogModule, MatIconModule],
    template: `
    <div class="confirm-cancel-dialog">
      <div class="confirm-cancel-header">
        <mat-icon class="warn-icon">warning_amber</mat-icon>
        <h2>Discard import?</h2>
      </div>
      <p>You have an active import in progress. Closing now will discard all staged data. Are you sure?</p>
      <div class="confirm-cancel-actions">
        <button mat-stroked-button mat-dialog-close="keep">Continue Import</button>
        <button mat-raised-button color="warn" mat-dialog-close="discard">Discard &amp; Close</button>
      </div>
    </div>
  `,
    styles: [`
    .confirm-cancel-dialog { padding: 24px; max-width: 420px; }
    .confirm-cancel-header { display: flex; align-items: center; gap: 12px; margin-bottom: 8px; }
    .warn-icon { color: #bf6000; font-size: 28px; width: 28px; height: 28px; }
    h2 { margin: 0; }
    p { color: var(--mat-sys-on-surface-variant); margin: 0 0 20px; }
    .confirm-cancel-actions { display: flex; justify-content: flex-end; gap: 10px; }
  `]
})
export class OfxCancelConfirmDialogComponent { }

// ── Main Component ───────────────────────────────────────────────────────────

@Component({
    selector: 'app-ofx-import',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ReactiveFormsModule,
        MatStepperModule,
        MatButtonModule,
        MatIconModule,
        MatFormFieldModule,
        MatSelectModule,
        MatInputModule,
        MatTableModule,
        MatTabsModule,
        MatSnackBarModule,
        MatTooltipModule,
        MatProgressSpinnerModule,
        MatButtonToggleModule,
        MatCardModule,
        MatDividerModule,
        MatDialogModule,
        MatCheckboxModule
    ],
    templateUrl: './ofx-import.component.html',
    styleUrl: './ofx-import.component.scss'
})
export class OfxImportComponent implements OnInit {
    private fb = inject(FormBuilder);
    private ofxService = inject(OfxImportService);
    private financeService = inject(FinanceService);
    private keywordRuleService = inject(AccountKeywordRuleService);
    private snackBar = inject(MatSnackBar);
    private dialog = inject(MatDialog);
    private dialogRef = inject<MatDialogRef<OfxImportComponent>>(MatDialogRef);
    data = inject(MAT_DIALOG_DATA);


    // ── Step 1 ──────────────────────────────────────────────────────────────────
    uploadForm: FormGroup;
    accounts: AccountDto[] = [];
    selectedFile: File | null = null;
    uploading = false;

    // ── Step 2 ──────────────────────────────────────────────────────────────────
    importFile: OfxImportedTransactionsFileDto | null = null;
    reviewData: OfxStagedImportReviewDto | null = null;
    loadingReview = false;
    importWarning: string | null = null;

    validDataSource = new MatTableDataSource<OfxStagingRowDto>();
    duplicateDataSource = new MatTableDataSource<OfxStagingRowDto>();
    alreadyImportedDataSource = new MatTableDataSource<OfxStagingRowDto>();
    errorDataSource = new MatTableDataSource<OfxStagingRowDto>();

    validSearch = '';
    duplicateSearch = '';
    alreadyImportedSearch = '';
    errorSearch = '';

    displayedValidColumns = ['select', 'date', 'description', 'amountRaw', 'currency', 'counterAccount', 'keyword', 'finalType', 'category'];
    displayedDuplicateColumns = ['date', 'description', 'amountRaw', 'counterAccount', 'status', 'actions'];
    displayedAlreadyImportedColumns = ['date', 'description', 'amountRaw', 'status', 'actions'];
    displayedErrorColumns = ['date', 'description', 'amountRaw', 'errorReason'];

    // ── Bulk selection ───────────────────────────────────────────────────────────
    selectedRowIds = new Set<string>();
    bulkCounterAccountId = '';

    // ── Keyword state ────────────────────────────────────────────────────────────
    keywordInputs: Record<string, string> = {};
    addingKeyword: Record<string, boolean> = {};

    // ── Step 3 ──────────────────────────────────────────────────────────────────
    committing = false;

    constructor() {
        const data = this.data;

        this.uploadForm = this.fb.group({
            accountId: [data?.preselectedAccountId ?? null, Validators.required],
            allowDuplicates: [false]
        });
        if (data?.preselectedAccountId) {
            this.uploadForm.get('accountId')!.disable();
        }
    }

    ngOnInit(): void {
        this.financeService.getAccounts().subscribe({
            next: (accounts) => this.accounts = accounts,
            error: () => this.snackBar.open('Failed to load accounts', 'Close', { duration: 4000 })
        });
    }

    // ── Cancel guard ─────────────────────────────────────────────────────────────

    /** Called when user clicks the × button in the dialog header. */
    onCancelAttempt(): void {
        if (!this.importFile) {
            this.dialogRef.close(false);
            return;
        }
        const confirmRef = this.dialog.open(OfxCancelConfirmDialogComponent, {
            disableClose: true,
            panelClass: 'slim-dialog'
        });
        confirmRef.afterClosed().subscribe((result: string) => {
            if (result === 'discard') {
                this.dialogRef.close(false);
            }
        });
    }

    // ── File Selection ───────────────────────────────────────────────────────────

    onFileSelected(event: Event): void {
        const input = event.target as HTMLInputElement;
        const file = input.files?.[0];
        if (file) {
            if (!file.name.toLowerCase().endsWith('.ofx')) {
                this.snackBar.open('Only .ofx files are supported', 'Close', { duration: 4000 });
                input.value = '';
                return;
            }
            this.selectedFile = file;
        }
    }

    clearFile(): void {
        this.selectedFile = null;
    }

    // ── Step 1 → Upload ─────────────────────────────────────────────────────────

    upload(stepper: any): void {
        if (!this.selectedFile || this.uploadForm.invalid) return;

        this.uploading = true;
        const { accountId, allowDuplicates } = this.uploadForm.value;

        this.ofxService.upload(this.selectedFile, accountId, allowDuplicates).subscribe({
            next: (file) => {
                this.importFile = file;
                this.importWarning = file.importWarning ?? null;
                this.uploading = false;
                this.loadReview(file.id, stepper);
            },
            error: (err) => {
                this.uploading = false;
                const msg = err?.error?.error ?? 'Upload failed. Please try again.';
                this.snackBar.open(msg, 'Close', { duration: 6000 });
            }
        });
    }

    // ── Step 2 → Review ─────────────────────────────────────────────────────────

    private loadReview(fileId: string, stepper?: any): void {
        this.loadingReview = true;
        if (stepper) stepper.next();
        this.ofxService.getStagedImport(fileId).subscribe({
            next: (data) => {
                this.reviewData = data;
                this.importWarning = data.file.importWarning ?? this.importWarning;
                this.validDataSource.data = data.validRows;
                this.duplicateDataSource.data = data.duplicateRows;
                this.alreadyImportedDataSource.data = data.alreadyImportedRows;
                this.errorDataSource.data = data.errorRows;
                this.loadingReview = false;
            },
            error: () => {
                this.loadingReview = false;
                this.snackBar.open('Failed to load review data', 'Close', { duration: 5000 });
            }
        });
    }

    get isAllAlreadyImported(): boolean {
        return !!this.reviewData &&
            this.reviewData.file.validRows === 0 &&
            this.reviewData.file.alreadyImportedRows > 0;
    }

    applyFilter(source: MatTableDataSource<OfxStagingRowDto>, term: string): void {
        source.filterPredicate = (row, f) => row.description.toLowerCase().includes(f);
        source.filter = term.trim().toLowerCase();
    }

    updateRowCounterAccount(row: OfxStagingRowDto, accountId: string): void {
        if (!this.importFile) return;
        const request: UpdateStagingRowRequest = {
            counterAccountId: accountId || undefined,
            categoryId: row.categoryId
        };

        this.ofxService.updateStagingRow(this.importFile.id, row.id, request).subscribe({
            next: (updated) => {
                row.counterAccountId = updated.counterAccountId;
                row.counterAccountName = updated.counterAccountName;
                row.inferredType = updated.inferredType;
            },
            error: () => this.snackBar.open('Failed to update row', 'Close', { duration: 3000 })
        });
    }

    addKeywordForRow(row: OfxStagingRowDto, keyword: string): void {
        if (!row.counterAccountId) {
            this.snackBar.open('Select a counter account first', 'Close', { duration: 3000 });
            return;
        }
        const trimmed = keyword?.trim();
        if (!trimmed) return;

        this.addingKeyword[row.id] = true;

        const request: CreateAccountKeywordRuleRequest = {
            accountId: row.counterAccountId,
            keyword: trimmed,
            matchType: KeywordMatchType.Contains,
            isCaseSensitive: false
        };

        this.keywordRuleService.create(request).subscribe({
            next: () => {
                this.addingKeyword[row.id] = false;
                this.keywordInputs[row.id] = '';

                const lowerKw = trimmed.toLowerCase();
                const matches = this.validDataSource.data.filter(r =>
                    r.id !== row.id &&
                    !r.counterAccountId &&
                    r.description.toLowerCase().includes(lowerKw)
                );
                matches.forEach(r => this.updateRowCounterAccount(r, row.counterAccountId!));

                const msg = matches.length > 0
                    ? `Keyword saved. Applied to ${matches.length} other row${matches.length > 1 ? 's' : ''}.`
                    : 'Keyword saved.';
                this.snackBar.open(msg, 'Close', { duration: 4000 });
            },
            error: () => {
                this.addingKeyword[row.id] = false;
                this.snackBar.open('Failed to save keyword', 'Close', { duration: 3000 });
            }
        });
    }

    forceImport(row: OfxStagingRowDto): void {
        if (!this.importFile) return;
        const request: UpdateStagingRowRequest = {
            counterAccountId: row.counterAccountId,
            categoryId: row.categoryId,
            ignoreDuplicateWarning: true
        };

        this.ofxService.updateStagingRow(this.importFile.id, row.id, request).subscribe({
            next: () => {
                this.snackBar.open('Transaction marked for import.', 'Close', { duration: 3000 });
                this.loadReview(this.importFile!.id);
            },
            error: () => this.snackBar.open('Failed to flag transaction for import.', 'Close', { duration: 3000 })
        });
    }

    /** Returns all accounts except the primary import account — used for the counter dropdown. */
    getCounterAccounts(): AccountDto[] {
        if (!this.importFile) return this.accounts;
        return this.accounts.filter(a => a.id !== this.importFile!.accountId);
    }

    getCounterAccountValue(row: OfxStagingRowDto): string {
        return row.counterAccountId ?? '';
    }

    /** Updates the counter account for a row based on its direction. */
    updateCounterAccount(row: OfxStagingRowDto, accountId: string): void {
        this.updateRowCounterAccount(row, accountId);
    }

    /** Toggles selection of a single row. */
    toggleRowSelection(rowId: string): void {
        if (this.selectedRowIds.has(rowId)) {
            this.selectedRowIds.delete(rowId);
        } else {
            this.selectedRowIds.add(rowId);
        }
    }

    /** Assigns the bulk counter account to all selected valid rows. */
    applyBulkCounterAccount(): void {
        if (!this.bulkCounterAccountId || this.selectedRowIds.size === 0) return;
        const rows = this.validDataSource.data.filter(r => this.selectedRowIds.has(r.id));
        rows.forEach(row => this.updateCounterAccount(row, this.bulkCounterAccountId));
        this.selectedRowIds.clear();
        this.bulkCounterAccountId = '';
    }

    isInflow(row: OfxStagingRowDto): boolean {
        return row.amountRaw > 0;
    }

    downloadErrorsCsv(): void {
        if (!this.reviewData?.errorRows.length) return;
        const headers = ['Date', 'Description', 'Amount', 'Error Reason'];
        const rows = this.reviewData.errorRows.map(r => [
            r.transactionDate.split('T')[0],
            `"${r.description.replace(/"/g, '""')}"`,
            r.amountRaw.toString(),
            `"${(r.errorReason ?? '').replace(/"/g, '""')}"`
        ]);
        const csv = [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
        const blob = new Blob([csv], { type: 'text/csv' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'ofx_import_errors.csv';
        a.click();
        URL.revokeObjectURL(url);
    }

    // ── Step 3 → Commit ─────────────────────────────────────────────────────────

    commit(): void {
        if (!this.importFile) return;
        this.committing = true;

        this.ofxService.commitImport(this.importFile.id).subscribe({
            next: () => {
                this.committing = false;
                this.snackBar.open(
                    `Import complete! ${this.importFile!.validRows} transactions added.`,
                    'Close',
                    { duration: 5000 }
                );
                this.dialogRef.close(true);
            },
            error: (err) => {
                this.committing = false;
                const msg = err?.error?.error ?? 'Commit failed. Please try again.';
                this.snackBar.open(msg, 'Close', { duration: 6000 });
            }
        });
    }

    formatAmount(val: number): string {
        return (val >= 0 ? '+' : '') + val.toFixed(2);
    }
}
