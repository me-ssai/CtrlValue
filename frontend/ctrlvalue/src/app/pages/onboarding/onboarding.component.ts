import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatStepperModule, MatStepper } from '@angular/material/stepper';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatListModule } from '@angular/material/list';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { AuthService } from '../../services/auth.service';
import { EntityService } from '../../services/entity.service';
import { FinanceService } from '../../services/finance.service';
import { CreateAccountRequest } from '../../models/api.models';
import { AccountType, AssetClass, LiquidityClass } from '../../services/api.generated';
import { AU_BANKS, AU_BROKERS, AU_LENDERS, AU_SUPER_FUNDS, filterInstitutions } from '../../shared/institution-suggestions';

interface AddedAccount {
    id: string;
    name: string;
    institution?: string;
    assetClass: string;
    accountType: string;
}

@Component({
    selector: 'app-onboarding',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatStepperModule,
        MatButtonModule,
        MatIconModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatCardModule,
        MatChipsModule,
        MatDividerModule,
        MatProgressBarModule,
        MatSnackBarModule,
        MatTooltipModule,
        MatListModule,
        MatDatepickerModule,
        MatNativeDateModule,
        MatAutocompleteModule
    ],
    templateUrl: './onboarding.component.html',
    styleUrls: ['./onboarding.component.scss']
})
export class OnboardingComponent implements OnInit {
    private fb = inject(FormBuilder);
    private authService = inject(AuthService);
    private entityService = inject(EntityService);
    private financeService = inject(FinanceService);
    private router = inject(Router);
    private snackBar = inject(MatSnackBar);

    @ViewChild('stepper') stepper!: MatStepper;

    // Step 1 — Welcome
    workspaceForm: FormGroup;

    // Step 2 — Bank Accounts
    bankAccountForm: FormGroup;
    addedBankAccounts: AddedAccount[] = [];

    // Step 3 — Assets
    assetForm: FormGroup;
    selectedAssetType: string | null = null;
    addedAssets: AddedAccount[] = [];
    assetTypeCards = [
        { type: 'PROPERTY', label: 'Real Estate', icon: 'home', assetClass: 'PROPERTY' },
        { type: 'VEHICLE', label: 'Vehicle', icon: 'directions_car', assetClass: 'VEHICLE' },
        { type: 'SUPER', label: 'Superannuation', icon: 'work', assetClass: 'SUPER' },
        { type: 'OTHER', label: 'Other Asset', icon: 'inventory_2', assetClass: 'OTHER' }
    ];

    // Step 4 — Liabilities
    liabilityForm: FormGroup;
    selectedLiabilityType: string | null = null;
    addedLiabilities: AddedAccount[] = [];
    liabilityTypeCards = [
        { type: 'MORTGAGE', label: 'Mortgage / Home Loan', icon: 'house', assetClass: 'PROPERTY' },
        { type: 'CREDIT_CARD', label: 'Credit Card', icon: 'credit_card', assetClass: 'CASH' },
        { type: 'PERSONAL_LOAN', label: 'Personal Loan', icon: 'request_quote', assetClass: 'CASH' },
        { type: 'OTHER', label: 'Other Liability', icon: 'receipt_long', assetClass: 'OTHER' }
    ];

    // Step 5 — Investing
    investmentForm: FormGroup;
    selectedInvestmentType: string | null = null;
    addedInvestments: AddedAccount[] = [];
    investmentTypeCards = [
        { type: 'STOCK', label: 'Stocks / ETFs', icon: 'candlestick_chart', assetClass: 'STOCK' },
        { type: 'METAL', label: 'Precious Metals', icon: 'diamond', assetClass: 'METAL' },
        { type: 'CRYPTO', label: 'Crypto', icon: 'currency_bitcoin', assetClass: 'CRYPTO' }
    ];

    filteredBankInstitutions: string[] = AU_BANKS;
    filteredAssetInstitutions: string[] = AU_BANKS;
    filteredLiabilityInstitutions: string[] = AU_LENDERS;
    filteredInvestInstitutions: string[] = AU_BROKERS;

    loading = false;
    savingWorkspace = false;
    deletingId: string | null = null;
    currentUser = this.authService.currentUser;
    currencies = ['AUD', 'USD', 'EUR', 'GBP', 'NZD', 'CAD', 'JPY', 'SGD', 'HKD', 'CHF'];

    constructor() {
        const entity = this.entityService.currentEntity;

        this.workspaceForm = this.fb.group({
            workspaceName: [entity?.name || `${this.currentUser?.firstName}'s Finances`, Validators.required],
            baseCurrency: [entity?.baseCurrency || 'AUD', Validators.required]
        });

        this.bankAccountForm = this.fb.group({
            name: ['', Validators.required],
            institution: [''],
            accountNumber: [''],
            startingBalance: [0],
            startingBalanceDate: [new Date()]
        });

        this.assetForm = this.fb.group({
            name: ['', Validators.required],
            institution: [''],
            startingBalance: [0],
            startingBalanceDate: [new Date()]
        });

        this.liabilityForm = this.fb.group({
            name: ['', Validators.required],
            institution: [''],
            startingBalance: [0],
            startingBalanceDate: [new Date()]
        });

        this.investmentForm = this.fb.group({
            name: ['', Validators.required],
            institution: [''],
            startingBalance: [0],
            startingBalanceDate: [new Date()]
        });

        this.bankAccountForm.get('institution')!.valueChanges.subscribe(v =>
            this.filteredBankInstitutions = filterInstitutions(AU_BANKS, v));

        this.assetForm.get('institution')!.valueChanges.subscribe(v =>
            this.filteredAssetInstitutions = filterInstitutions(this.getAssetInstitutionList(), v));

        this.liabilityForm.get('institution')!.valueChanges.subscribe(v =>
            this.filteredLiabilityInstitutions = filterInstitutions(AU_LENDERS, v));

        this.investmentForm.get('institution')!.valueChanges.subscribe(v =>
            this.filteredInvestInstitutions = filterInstitutions(AU_BROKERS, v));
    }

    private getAssetInstitutionList(): string[] {
        return this.selectedAssetType === 'SUPER' ? AU_SUPER_FUNDS : AU_BANKS;
    }

    ngOnInit(): void {
        this.loadExistingAccounts();
    }

    private loadExistingAccounts(): void {
        const INVESTMENT_CLASSES = ['STOCK', 'METAL', 'CRYPTO'];
        const ASSET_ONLY_CLASSES = ['PROPERTY', 'VEHICLE', 'SUPER', 'OTHER'];

        this.financeService.getAccounts().subscribe({
            next: (accounts) => {
                this.addedBankAccounts = accounts
                    .filter(a => a.accountType === 'ASSET' && a.assetClass === 'CASH')
                    .map(a => ({ id: a.id!, name: a.name ?? '', institution: a.institution ?? undefined, assetClass: a.assetClass!, accountType: a.accountType! }));

                this.addedAssets = accounts
                    .filter(a => a.accountType === 'ASSET' && ASSET_ONLY_CLASSES.includes(a.assetClass!))
                    .map(a => ({ id: a.id!, name: a.name ?? '', institution: a.institution ?? undefined, assetClass: a.assetClass!, accountType: a.accountType! }));

                this.addedLiabilities = accounts
                    .filter(a => a.accountType === 'LIABILITY')
                    .map(a => ({ id: a.id!, name: a.name ?? '', institution: a.institution ?? undefined, assetClass: a.assetClass!, accountType: a.accountType! }));

                this.addedInvestments = accounts
                    .filter(a => a.accountType === 'ASSET' && INVESTMENT_CLASSES.includes(a.assetClass!))
                    .map(a => ({ id: a.id!, name: a.name ?? '', institution: a.institution ?? undefined, assetClass: a.assetClass!, accountType: a.accountType! }));
            }
        });
    }

    // ── Step 1: Workspace ─────────────────────────────────────────────────────

    saveWorkspace(): void {
        if (this.workspaceForm.invalid) return;
        const entity = this.entityService.currentEntity;
        if (!entity) {
            this.stepper.next();
            return;
        }
        this.savingWorkspace = true;
        this.entityService.updateEntity(entity.id, {
            name: this.workspaceForm.value.workspaceName,
            baseCurrency: this.workspaceForm.value.baseCurrency,
            country: entity.country
        }).subscribe({
            next: (updated) => {
                this.entityService.setCurrentEntity(updated);
                this.savingWorkspace = false;
                this.stepper.next();
            },
            error: () => {
                this.savingWorkspace = false;
                this.stepper.next(); // Don't block onboarding on this
            }
        });
    }

    // ── Step 2: Bank Accounts ─────────────────────────────────────────────────

    addBankAccount(): void {
        if (this.bankAccountForm.invalid) return;
        this.loading = true;
        const v = this.bankAccountForm.value;
        const request: CreateAccountRequest = {
            name: v.name,
            accountType: AccountType.ASSET,
            assetClass: AssetClass.CASH,
            liquidityClass: LiquidityClass.LIQUID,
            currency: this.workspaceForm.value.baseCurrency || 'AUD',
            institution: v.institution || undefined,
            accountNumber: v.accountNumber || undefined,
            startingBalance: v.startingBalance ?? 0,
            startingBalanceDate: v.startingBalanceDate ?? new Date()
        };
        this.financeService.createAccount(request).subscribe({
            next: (account) => {
                this.addedBankAccounts.push({ id: account.id!, name: v.name, institution: v.institution, assetClass: 'CASH', accountType: 'ASSET' });
                this.bankAccountForm.reset({ startingBalance: 0, startingBalanceDate: new Date() });
                this.loading = false;
                this.snackBar.open(`"${v.name}" added`, 'OK', { duration: 2000 });
            },
            error: (err) => {
                this.loading = false;
                this.snackBar.open(err.error?.message || 'Failed to add account', 'Close', { duration: 4000 });
            }
        });
    }

    // ── Step 3: Assets ────────────────────────────────────────────────────────

    selectAssetType(type: string): void {
        this.selectedAssetType = type;
        this.assetForm.reset({ startingBalance: 0, startingBalanceDate: new Date() });
        const card = this.assetTypeCards.find(c => c.type === type);
        this.assetForm.patchValue({ name: card?.label || '' });
        this.filteredAssetInstitutions = this.getAssetInstitutionList();
    }

    addAsset(): void {
        if (!this.selectedAssetType || this.assetForm.invalid) return;
        this.loading = true;
        const v = this.assetForm.value;
        const card = this.assetTypeCards.find(c => c.type === this.selectedAssetType);
        const request: CreateAccountRequest = {
            name: v.name,
            accountType: AccountType.ASSET,
            assetClass: (card?.assetClass || 'OTHER') as AssetClass,
            liquidityClass: card?.assetClass === 'PROPERTY' ? LiquidityClass.ILLIQUID : LiquidityClass.SEMI_LIQUID,
            currency: this.workspaceForm.value.baseCurrency || 'AUD',
            institution: v.institution || undefined,
            startingBalance: v.startingBalance ?? 0,
            startingBalanceDate: v.startingBalanceDate ?? new Date()
        };
        this.financeService.createAccount(request).subscribe({
            next: (account) => {
                this.addedAssets.push({ id: account.id!, name: v.name, institution: v.institution, assetClass: card?.assetClass || 'OTHER', accountType: 'ASSET' });
                this.selectedAssetType = null;
                this.assetForm.reset({ startingBalance: 0, startingBalanceDate: new Date() });
                this.loading = false;
                this.snackBar.open(`"${v.name}" added`, 'OK', { duration: 2000 });
            },
            error: (err) => {
                this.loading = false;
                this.snackBar.open(err.error?.message || 'Failed to add asset', 'Close', { duration: 4000 });
            }
        });
    }

    // ── Step 4: Liabilities ───────────────────────────────────────────────────

    selectLiabilityType(type: string): void {
        this.selectedLiabilityType = type;
        this.liabilityForm.reset({ startingBalance: 0, startingBalanceDate: new Date() });
        const card = this.liabilityTypeCards.find(c => c.type === type);
        this.liabilityForm.patchValue({ name: card?.label || '' });
    }

    addLiability(): void {
        if (!this.selectedLiabilityType || this.liabilityForm.invalid) return;
        this.loading = true;
        const v = this.liabilityForm.value;
        const card = this.liabilityTypeCards.find(c => c.type === this.selectedLiabilityType);
        const request: CreateAccountRequest = {
            name: v.name,
            accountType: AccountType.LIABILITY,
            assetClass: (card?.assetClass || 'CASH') as AssetClass,
            liquidityClass: LiquidityClass.LIQUID,
            currency: this.workspaceForm.value.baseCurrency || 'AUD',
            institution: v.institution || undefined,
            startingBalance: v.startingBalance ?? 0,
            startingBalanceDate: v.startingBalanceDate ?? new Date()
        };
        this.financeService.createAccount(request).subscribe({
            next: (account) => {
                this.addedLiabilities.push({ id: account.id!, name: v.name, institution: v.institution, assetClass: card?.assetClass || 'CASH', accountType: 'LIABILITY' });
                this.selectedLiabilityType = null;
                this.liabilityForm.reset({ startingBalance: 0, startingBalanceDate: new Date() });
                this.loading = false;
                this.snackBar.open(`"${v.name}" added`, 'OK', { duration: 2000 });
            },
            error: (err) => {
                this.loading = false;
                this.snackBar.open(err.error?.message || 'Failed to add liability', 'Close', { duration: 4000 });
            }
        });
    }

    // ── Step 5: Investing ─────────────────────────────────────────────────────

    selectInvestmentType(type: string): void {
        this.selectedInvestmentType = type;
        this.investmentForm.reset({ startingBalance: 0, startingBalanceDate: new Date() });
        const card = this.investmentTypeCards.find(c => c.type === type);
        this.investmentForm.patchValue({ name: card?.label || '' });
    }

    addInvestmentAccount(): void {
        if (!this.selectedInvestmentType || this.investmentForm.invalid) return;
        this.loading = true;
        const v = this.investmentForm.value;
        const card = this.investmentTypeCards.find(c => c.type === this.selectedInvestmentType);
        const request: CreateAccountRequest = {
            name: v.name,
            accountType: AccountType.ASSET,
            assetClass: (card?.assetClass || 'STOCK') as AssetClass,
            liquidityClass: LiquidityClass.SEMI_LIQUID,
            currency: this.workspaceForm.value.baseCurrency || 'AUD',
            institution: v.institution || undefined,
            startingBalance: v.startingBalance ?? 0,
            startingBalanceDate: v.startingBalanceDate ?? new Date()
        };
        this.financeService.createAccount(request).subscribe({
            next: (account) => {
                this.addedInvestments.push({ id: account.id!, name: v.name, institution: v.institution, assetClass: card?.assetClass || 'STOCK', accountType: 'ASSET' });
                this.selectedInvestmentType = null;
                this.investmentForm.reset({ startingBalance: 0, startingBalanceDate: new Date() });
                this.loading = false;
                this.snackBar.open(`"${v.name}" added`, 'OK', { duration: 2000 });
            },
            error: (err) => {
                this.loading = false;
                this.snackBar.open(err.error?.message || 'Failed to add investment account', 'Close', { duration: 4000 });
            }
        });
    }

    // ── Remove account ────────────────────────────────────────────────────────

    removeAccount(account: AddedAccount, list: AddedAccount[]): void {
        this.deletingId = account.id;
        this.financeService.deleteAccount(account.id).subscribe({
            next: () => {
                const idx = list.findIndex(a => a.id === account.id);
                if (idx !== -1) list.splice(idx, 1);
                this.deletingId = null;
            },
            error: () => {
                this.deletingId = null;
                this.snackBar.open('Failed to remove account', 'Close', { duration: 4000 });
            }
        });
    }

    // ── Step 6: Complete ──────────────────────────────────────────────────────

    get totalAdded(): number {
        return this.addedBankAccounts.length + this.addedAssets.length + this.addedLiabilities.length + this.addedInvestments.length;
    }

    finish(): void {
        if (this.authService.isOnboardingComplete) {
            this.router.navigate(['/dashboard']);
            return;
        }
        this.loading = true;
        this.authService.completeOnboarding().subscribe({
            next: () => {
                this.loading = false;
                this.router.navigate(['/dashboard']);
            },
            error: () => {
                // Even if the API call fails, update local state and navigate
                const user = this.authService.currentUser;
                if (user) {
                    (user as any).onboardingCompleted = true;
                }
                this.loading = false;
                this.router.navigate(['/dashboard']);
            }
        });
    }

    skipToEnd(): void {
        this.finish();
    }

    getAssetClassIcon(assetClass: string): string {
        const icons: Record<string, string> = {
            CASH: 'account_balance', STOCK: 'candlestick_chart', ETF: 'candlestick_chart',
            METAL: 'diamond', VEHICLE: 'directions_car', PROPERTY: 'home',
            SUPER: 'work', BUSINESS: 'business', CRYPTO: 'currency_bitcoin', OTHER: 'inventory_2'
        };
        return icons[assetClass] || 'account_circle';
    }

    getCardLabel(cards: { type: string; label: string }[], type: string | null): string {
        return cards.find(c => c.type === type)?.label ?? '';
    }

    getCardIcon(cards: { type: string; icon: string }[], type: string | null): string {
        return cards.find(c => c.type === type)?.icon ?? 'inventory_2';
    }
}
