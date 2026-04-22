import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { environment } from '@env/environment';
import {
    Client,
    AccountDto,
    CreateAccountRequest,
    UpdateAccountRequest,
    AccountSummaryDto,
    TransactionDto,
    CreateTransactionRequest,
    UpdateTransactionRequest,
    CategoryDto,
    CreateCategoryRequest,
    UpdateCategoryRequest,
    AccountDeletionImpactDto
} from './api.generated'; // Import from generated file directly or via models

export interface BulkDeleteTransactionsRequest {
    transactionIds: string[];
}

@Injectable({ providedIn: 'root' })
export class FinanceService {

    constructor(private client: Client, private http: HttpClient) { }

    // ═══════════════════════════════════════════════════════════════════════════
    // Accounts
    // ═══════════════════════════════════════════════════════════════════════════

    getAccounts(type?: string): Observable<AccountDto[]> {
        return this.client.accountsAll(type);
    }

    getAccount(id: string): Observable<AccountDto> {
        return this.client.accountsGET(id);
    }

    createAccount(request: CreateAccountRequest): Observable<AccountDto> {
        return this.client.accountsPOST(request).pipe(
            switchMap(account => this.client.recalculateAll().pipe(map(() => account)))
        );
    }

    updateAccount(id: string, request: UpdateAccountRequest): Observable<AccountDto> {
        return this.client.accountsPUT(id, request);
    }

    deleteAccount(id: string): Observable<void> {
        return this.client.accountsDELETE(id);
    }

    recalculateAllBalances(): Observable<void> {
        return this.client.recalculateAll();
    }

    getAccountSummary(): Observable<AccountSummaryDto> {
        return this.client.summary();
    }

    getAccountDeletionImpact(id: string): Observable<AccountDeletionImpactDto> {
        return this.client.deletionImpact(id);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Transactions
    // ═══════════════════════════════════════════════════════════════════════════

    getTransactions(startDate?: string, endDate?: string): Observable<TransactionDto[]> {
        const start = startDate ? new Date(startDate) : undefined;
        const end = endDate ? new Date(endDate) : undefined;
        return this.client.transactionsAll(start, end);
    }

    getTransaction(id: string): Observable<TransactionDto> {
        return this.client.transactionsGET(id);
    }

    createTransaction(request: CreateTransactionRequest): Observable<TransactionDto> {
        return this.client.transactionsPOST(request);
    }

    updateTransaction(id: string, request: UpdateTransactionRequest): Observable<TransactionDto> {
        return this.client.transactionsPUT(id, request);
    }

    deleteTransaction(id: string): Observable<void> {
        return this.client.transactionsDELETE(id);
    }

    // Aliases for compatibility if needed (deprecated)
    getTransactionsNew(startDate?: string, endDate?: string): Observable<TransactionDto[]> {
        return this.getTransactions(startDate, endDate);
    }

    getTransactionNew(id: string): Observable<TransactionDto> {
        return this.getTransaction(id);
    }

    createTransactionNew(request: CreateTransactionRequest): Observable<TransactionDto> {
        return this.createTransaction(request);
    }

    updateTransactionNew(id: string, request: UpdateTransactionRequest): Observable<TransactionDto> {
        return this.updateTransaction(id, request);
    }

    deleteTransactionNew(id: string): Observable<void> {
        return this.deleteTransaction(id);
    }

    getTransactionsByAccount(accountId: string): Observable<TransactionDto[]> {
        return this.client.byAccount(accountId);
    }

    bulkDelete(ids: string[]): Observable<void> {
        const body: BulkDeleteTransactionsRequest = { transactionIds: ids };
        return this.http.post<void>(`${environment.apiUrl}/Transactions/bulk-delete`, body);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Instruments
    // ═══════════════════════════════════════════════════════════════════════════

    getInstruments(): Observable<any[]> {
        return this.client.instrumentsAll();
    }

    getInstrument(id: string): Observable<any> {
        return this.client.instrumentsGET(id);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Categories
    // ═══════════════════════════════════════════════════════════════════════════

    getCategories(type?: string): Observable<CategoryDto[]> {
        return this.client.categoriesAll(type);
    }

    getCategory(id: string): Observable<CategoryDto> {
        return this.client.categoriesGET(id);
    }

    createCategory(request: CreateCategoryRequest): Observable<CategoryDto> {
        return this.client.categoriesPOST(request);
    }

    updateCategory(id: string, request: UpdateCategoryRequest): Observable<CategoryDto> {
        return this.client.categoriesPUT(id, request);
    }

    deleteCategory(id: string): Observable<void> {
        return this.client.categoriesDELETE(id);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // OLD METHODS (Mapped to new Client methods)
    // ═══════════════════════════════════════════════════════════════════════════

    getAssets(): Observable<any[]> {
        return this.getAccounts('ASSET').pipe(
            map(accounts => accounts.map(a => ({
                ...a,
                currentValue: 0,
                category: a.assetClass,
                description: a.notes
            })))
        );
    }

    getAsset(id: string): Observable<any> {
        return this.getAccount(id).pipe(
            map(a => ({
                ...a,
                currentValue: 0,
                category: a.assetClass,
                description: a.notes
            }))
        );
    }

    createAsset(request: any): Observable<any> {
        // Map CreateAssetRequest to CreateAccountRequest if needed, or assume compatible
        return this.createAccount(request);
    }

    updateAsset(id: string, request: any): Observable<any> {
        return this.updateAccount(id, request);
    }

    deleteAsset(id: string): Observable<void> {
        return this.deleteAccount(id);
    }

    getLiabilities(): Observable<any[]> {
        return this.getAccounts('LIABILITY').pipe(
            map(accounts => accounts.map(a => ({
                ...a,
                currentValue: 0,
                outstandingAmount: 0,
                category: a.accountType, // liabilityClass doesn't exist
                description: a.notes
            })))
        );
    }

    getLiability(id: string): Observable<any> {
        return this.getAccount(id).pipe(
            map(a => ({
                ...a,
                currentValue: 0,
                outstandingAmount: 0,
                category: a.accountType,
                description: a.notes
            }))
        );
    }

    createLiability(request: any): Observable<any> {
        return this.createAccount(request);
    }

    updateLiability(id: string, request: any): Observable<any> {
        return this.updateAccount(id, request);
    }

    deleteLiability(id: string): Observable<void> {
        return this.deleteAccount(id);
    }

    // Assets/Liabilities should use getAccounts with type
    // If we need to support old getAssets call, we can map it:
    // getAssets(): Observable<any[]> { return this.getAccounts('ASSET'); }
    // But better to update components to use getAccounts.
}
