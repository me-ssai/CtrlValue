import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FormBuilder } from '@angular/forms';
import { of, Subject, throwError } from 'rxjs';

import { AccountDetailComponent } from './account-detail.component';
import { FinanceService } from '../../../services/finance.service';
import { IntelligenceService } from '../../../services/intelligence.service';
import { DeleteAccountConfirmDialogComponent } from '../delete-account-confirm-dialog/delete-account-confirm-dialog.component';
import { Account } from '../../../models/api.models';

const mockAccount: Account = {
    id: 'acc-1', name: 'Test Account', accountType: 'ASSET',
    institution: 'ANZ', currentBalance: 5000, currency: 'AUD',
    assetClass: 'CASH', liquidityClass: 'LIQUID', isActive: true,
    startingBalance: 0, startingBalanceDate: new Date().toISOString()
} as unknown as Account;

describe('AccountDetailComponent', () => {
    let component: AccountDetailComponent;
    let fixture: ComponentFixture<AccountDetailComponent>;
    let financeServiceSpy: jasmine.SpyObj<FinanceService>;
    let routerSpy: jasmine.SpyObj<Router>;
    let snackBarSpy: jasmine.SpyObj<MatSnackBar>;
    let dialogSpy: jasmine.SpyObj<MatDialog>;

    beforeEach(() => {
        financeServiceSpy = jasmine.createSpyObj('FinanceService', [
            'getAccount', 'getTransactionsByAccount', 'updateAccount',
            'deleteAccount', 'deleteTransaction', 'bulkDelete'
        ]);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);
        snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);
        dialogSpy = jasmine.createSpyObj('MatDialog', ['open']);

        financeServiceSpy.getAccount.and.returnValue(of(mockAccount));
        financeServiceSpy.getTransactionsByAccount.and.returnValue(of([]));

        TestBed.configureTestingModule({
            imports: [AccountDetailComponent],
            providers: [
                FormBuilder,
                { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'acc-1' } } } },
                { provide: Router, useValue: routerSpy },
                { provide: FinanceService, useValue: financeServiceSpy },
                { provide: IntelligenceService, useValue: jasmine.createSpyObj('IntelligenceService', []) },
                { provide: MatSnackBar, useValue: snackBarSpy },
                { provide: MatDialog, useValue: dialogSpy },
            ],
            schemas: [NO_ERRORS_SCHEMA]
        });

        fixture = TestBed.createComponent(AccountDetailComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    describe('deleteAccount', () => {
        it('should open the confirmation dialog with the correct account data', () => {
            const afterClosed$ = new Subject<boolean>();
            dialogSpy.open.and.returnValue({ afterClosed: () => afterClosed$ } as unknown as MatDialogRef<any>);

            component.deleteAccount();

            expect(dialogSpy.open).toHaveBeenCalledWith(DeleteAccountConfirmDialogComponent, {
                width: '480px',
                data: { accountId: 'acc-1', accountName: 'Test Account' }
            });
        });

        it('should delete the account and navigate to /accounts on confirmation', () => {
            const afterClosed$ = new Subject<boolean>();
            dialogSpy.open.and.returnValue({ afterClosed: () => afterClosed$ } as unknown as MatDialogRef<any>);
            financeServiceSpy.deleteAccount.and.returnValue(of(void 0));

            component.deleteAccount();
            afterClosed$.next(true);

            expect(financeServiceSpy.deleteAccount).toHaveBeenCalledWith('acc-1');
            expect(snackBarSpy.open).toHaveBeenCalledWith('Account deleted', 'Close', { duration: 4000 });
            expect(routerSpy.navigate).toHaveBeenCalledWith(['/accounts']);
        });

        it('should not delete when dialog is dismissed', () => {
            const afterClosed$ = new Subject<boolean>();
            dialogSpy.open.and.returnValue({ afterClosed: () => afterClosed$ } as unknown as MatDialogRef<any>);

            component.deleteAccount();
            afterClosed$.next(false);

            expect(financeServiceSpy.deleteAccount).not.toHaveBeenCalled();
            expect(routerSpy.navigate).not.toHaveBeenCalled();
        });

        it('should show an error snackbar when deletion fails', () => {
            const afterClosed$ = new Subject<boolean>();
            dialogSpy.open.and.returnValue({ afterClosed: () => afterClosed$ } as unknown as MatDialogRef<any>);
            financeServiceSpy.deleteAccount.and.returnValue(throwError(() => new Error()));

            component.deleteAccount();
            afterClosed$.next(true);

            expect(snackBarSpy.open).toHaveBeenCalledWith('Failed to delete account', 'Close', { duration: 5000 });
            expect(routerSpy.navigate).not.toHaveBeenCalled();
        });
    });
});
