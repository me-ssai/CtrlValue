import { Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { forkJoin } from 'rxjs';

import { FinanceService } from '../../../services/finance.service';
import { IntelligenceService, CategorySuggestion } from '../../../services/intelligence.service';
import { CategoryKeywordRuleService } from '../../../services/category-keyword-rule.service';
import {
    Category,
    KeywordMatchType,
    Transaction,
    UpdateTransactionRequest
} from '../../../models/api.models';
import { CategoryType } from '../../../services/api.generated';

export interface QuickCategorizeDialogData {
    transaction: Transaction;
}

export interface QuickCategorizeDialogResult {
    categorizedCount: number;
}

@Component({
    selector: 'app-quick-categorize-dialog',
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
        MatSlideToggleModule,
        MatChipsModule,
        MatProgressSpinnerModule,
        MatTooltipModule,
        MatDividerModule,
        MatSnackBarModule
    ],
    templateUrl: './quick-categorize-dialog.component.html',
    styleUrl: './quick-categorize-dialog.component.scss'
})
export class QuickCategorizeDialogComponent implements OnInit {
    data = inject<QuickCategorizeDialogData>(MAT_DIALOG_DATA);
    private dialogRef = inject<MatDialogRef<QuickCategorizeDialogComponent, QuickCategorizeDialogResult | undefined>>(MatDialogRef);
    private fb = inject(FormBuilder);
    private financeService = inject(FinanceService);
    private intelligenceService = inject(IntelligenceService);
    private ruleService = inject(CategoryKeywordRuleService);
    private snackBar = inject(MatSnackBar);

    @ViewChild('categorySearchInput') categorySearchInput!: ElementRef<HTMLInputElement>;

    form!: FormGroup;
    categorySearchControl = new FormControl('');
    categories: Category[] = [];
    filteredCategories: Category[] = [];
    suggestion: CategorySuggestion | null = null;
    suggestionDismissed = false;

    // Inline new-category panel
    showNewCategoryPanel = false;
    newCategoryForm!: FormGroup;
    savingNewCategory = false;

    loadingCategories = true;
    saving = false;

    readonly matchTypes = [
        { value: KeywordMatchType.Contains, label: 'Contains' },
        { value: KeywordMatchType.Exact, label: 'Exact Match' },
        { value: KeywordMatchType.StartsWith, label: 'Starts With' },
        { value: KeywordMatchType.Regex, label: 'Regex' }
    ];

    readonly categoryTypes = [
        { value: CategoryType.EXPENSE, label: 'Expense' },
        { value: CategoryType.INCOME, label: 'Income' },
        { value: CategoryType.TRANSFER, label: 'Transfer' }
    ];

    ngOnInit(): void {
        const tx = this.data.transaction;
        const keyword = tx.merchant || tx.description || '';

        this.form = this.fb.group({
            categoryId: [null, Validators.required],
            saveAsRule: [true],
            keyword: [keyword, Validators.required],
            matchType: [KeywordMatchType.Contains]
        });

        this.newCategoryForm = this.fb.group({
            name: ['', [Validators.required, Validators.maxLength(100)]],
            categoryType: [CategoryType.EXPENSE, Validators.required]
        });

        this.categorySearchControl.valueChanges.subscribe(term => {
            this.filteredCategories = this.filterCategories(term ?? '');
        });

        // Load categories and suggestion in parallel
        forkJoin({
            categories: this.financeService.getCategories()
        }).subscribe({
            next: ({ categories }) => {
                this.categories = categories;
                this.filteredCategories = [...categories];
                this.loadingCategories = false;
            },
            error: () => {
                this.loadingCategories = false;
                this.snackBar.open('Failed to load categories', 'Close', { duration: 4000 });
            }
        });

        // Fetch suggestion non-blockingly
        const descForSuggest = tx.description || tx.merchant || '';
        if (descForSuggest) {
            this.intelligenceService.suggestCategory(descForSuggest).subscribe(suggestion => {
                if (suggestion && !this.form.get('categoryId')?.value) {
                    this.suggestion = suggestion;
                    this.form.get('categoryId')!.setValue(suggestion.categoryId);
                }
            });
        }
    }

    get saveAsRule(): boolean {
        return this.form.get('saveAsRule')?.value ?? false;
    }

    get showSuggestion(): boolean {
        return !!this.suggestion && !this.suggestionDismissed;
    }

    acceptSuggestion(): void {
        if (this.suggestion) {
            this.form.get('categoryId')!.setValue(this.suggestion.categoryId);
        }
        this.suggestionDismissed = true;
    }

    dismissSuggestion(): void {
        this.suggestion = null;
        this.suggestionDismissed = true;
    }

    toggleNewCategoryPanel(): void {
        this.showNewCategoryPanel = !this.showNewCategoryPanel;
        if (this.showNewCategoryPanel) {
            this.newCategoryForm.reset({ categoryType: CategoryType.EXPENSE });
        }
    }

    saveNewCategory(): void {
        if (this.newCategoryForm.invalid) {
            this.newCategoryForm.markAllAsTouched();
            return;
        }
        this.savingNewCategory = true;
        const v = this.newCategoryForm.value;
        this.financeService.createCategory({ name: v.name, categoryType: v.categoryType }).subscribe({
            next: (cat) => {
                this.categories = [...this.categories, cat];
                this.filteredCategories = this.filterCategories(this.categorySearchControl.value ?? '');
                this.form.get('categoryId')!.setValue(cat.id);
                this.showNewCategoryPanel = false;
                this.savingNewCategory = false;
            },
            error: (err) => {
                this.savingNewCategory = false;
                this.snackBar.open(err.error?.message || 'Failed to create category', 'Close', { duration: 4000 });
            }
        });
    }

    onSave(): void {
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }
        this.saving = true;
        const tx = this.data.transaction;
        const categoryId = this.form.get('categoryId')!.value;

        // Build full update request — spread existing fields, override only categoryId
        const request: UpdateTransactionRequest = {
            txnTime: tx.txnTime as any,
            description: tx.description ?? '',
            txnType: tx.txnType as any,
            amount: tx.amount ?? 0,
            currency: tx.currency ?? 'AUD',
            categoryId,
            accountId: tx.accountId ?? '',
            direction: tx.direction as any,
            counterAccountId: tx.counterAccountId ?? undefined,
            instrumentId: tx.instrumentId ?? undefined,
            quantity: tx.quantity ?? undefined,
            unitPrice: tx.unitPrice ?? undefined,
            fees: tx.fees ?? undefined,
            isTaxDeductible: tx.isTaxDeductible ?? false,
            tags: tx.tags ?? undefined,
            receiptUrl: tx.receiptUrl ?? undefined,
            externalId: tx.externalId ?? undefined,
            relatedTxnId: tx.relatedTxnId ?? undefined,
            merchant: tx.merchant ?? undefined
        };

        this.financeService.updateTransaction(tx.id!, request).subscribe({
            next: () => {
                if (this.saveAsRule) {
                    this.createKeywordRule(categoryId);
                } else {
                    this.saving = false;
                    this.dialogRef.close({ categorizedCount: 0 });
                }
            },
            error: (err) => {
                this.saving = false;
                this.snackBar.open(err.error?.message || 'Failed to categorize transaction', 'Close', { duration: 5000 });
            }
        });
    }

    private createKeywordRule(categoryId: string): void {
        const v = this.form.value;
        this.ruleService.create({
            categoryId,
            keyword: v.keyword,
            matchType: v.matchType,
            isCaseSensitive: false
        }).subscribe({
            next: () => {
                // Backend CreateAsync already ran ApplyRulesToWorkspaceAsync as a side-effect.
                // Call the dedicated endpoint to get the count for user feedback.
                this.intelligenceService.applyCategorizationRules().subscribe({
                    next: (result) => {
                        this.saving = false;
                        this.dialogRef.close({ categorizedCount: result.categorizedCount });
                    },
                    error: () => {
                        this.saving = false;
                        this.dialogRef.close({ categorizedCount: 0 });
                    }
                });
            },
            error: (err) => {
                this.saving = false;
                const msg = err.error?.message || err.error?.error || 'Failed to save keyword rule';
                this.snackBar.open(msg, 'Close', { duration: 5000 });
                // Do NOT close the dialog — let user fix and retry
            }
        });
    }

    onCategoryPanelOpened(): void {
        this.categorySearchControl.setValue('');
        setTimeout(() => this.categorySearchInput?.nativeElement.focus(), 50);
    }

    private filterCategories(term: string): Category[] {
        if (!term) return [...this.categories];
        const lower = term.toLowerCase();
        return this.categories.filter(c => c.name?.toLowerCase().includes(lower));
    }

    cancel(): void {
        this.dialogRef.close(undefined);
    }
}
