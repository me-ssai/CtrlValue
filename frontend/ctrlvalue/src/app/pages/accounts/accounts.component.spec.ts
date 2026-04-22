import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';

import { AccountsComponent } from './accounts.component';
import { FinanceService } from '../../services/finance.service';
import { Account } from '../../models/api.models';

const mockAsset: Account = {
    id: 'asset-1', name: 'Savings', accountType: 'ASSET',
    institution: 'ANZ', currentBalance: 5000, currency: 'AUD',
    assetClass: 'CASH', liquidityClass: 'LIQUID', isActive: true
} as Account;

const mockLiability: Account = {
    id: 'liab-1', name: 'Credit Card', accountType: 'LIABILITY',
    institution: 'CBA', currentBalance: -1000, currency: 'AUD',
    isActive: true
} as Account;

describe('AccountsComponent', () => {
    let component: AccountsComponent;
    let fixture: ComponentFixture<AccountsComponent>;
    let financeServiceSpy: jasmine.SpyObj<FinanceService>;
    let routerSpy: jasmine.SpyObj<Router>;
    let snackBarSpy: jasmine.SpyObj<MatSnackBar>;
    let dialogSpy: jasmine.SpyObj<MatDialog>;

    beforeEach(() => {
        financeServiceSpy = jasmine.createSpyObj('FinanceService', ['getAccounts']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);
        snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);
        dialogSpy = jasmine.createSpyObj('MatDialog', ['open']);

        financeServiceSpy.getAccounts.and.returnValue(of([mockAsset, mockLiability]));

        TestBed.configureTestingModule({
            imports: [AccountsComponent],
            providers: [
                { provide: FinanceService, useValue: financeServiceSpy },
                { provide: Router, useValue: routerSpy },
                { provide: MatSnackBar, useValue: snackBarSpy },
                { provide: MatDialog, useValue: dialogSpy },
            ],
            schemas: [NO_ERRORS_SCHEMA]
        });

        fixture = TestBed.createComponent(AccountsComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should load accounts on init', () => {
        expect(financeServiceSpy.getAccounts).toHaveBeenCalled();
        expect(component.accounts.length).toBe(2);
    });

    it('should separate asset and liability accounts', () => {
        expect(component.assetAccounts.length).toBe(1);
        expect(component.liabilityAccounts.length).toBe(1);
    });

    it('should navigate to account detail on openAccount', () => {
        component.openAccount('asset-1');
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/accounts', 'asset-1']);
    });

    it('should show error snackbar when loadAccounts fails', () => {
        financeServiceSpy.getAccounts.and.returnValue(throwError(() => new Error()));
        component.loadAccounts();
        expect(snackBarSpy.open).toHaveBeenCalledWith('Failed to load accounts', 'Close', { duration: 5000 });
    });

    it('should not have a deleteAccount method', () => {
        expect((component as any).deleteAccount).toBeUndefined();
    });
});
