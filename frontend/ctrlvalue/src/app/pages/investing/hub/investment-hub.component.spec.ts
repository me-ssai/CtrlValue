import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { Chart, ArcElement, DoughnutController, Tooltip, Legend } from 'chart.js';

// Register Chart.js types needed by the component template
Chart.register(ArcElement, DoughnutController, Tooltip, Legend);

import { InvestmentHubComponent } from './investment-hub.component';
import { PositionService, Position } from '../../../services/instrument.service';
import { PropertyService, Property } from '../../../services/property.service';
import { FinanceService } from '../../../services/finance.service';
import { AccountDto } from '../../../models/api.models';

describe('InvestmentHubComponent', () => {
    let component: InvestmentHubComponent;
    let fixture: ComponentFixture<InvestmentHubComponent>;
    let positionServiceSpy: jasmine.SpyObj<PositionService>;
    let propertyServiceSpy: jasmine.SpyObj<PropertyService>;
    let financeServiceSpy: jasmine.SpyObj<FinanceService>;

    const noPositions: Position[] = [];
    const noProperties: Property[] = [];

    beforeEach(async () => {
        positionServiceSpy = jasmine.createSpyObj('PositionService', ['getPositions']);
        propertyServiceSpy = jasmine.createSpyObj('PropertyService', ['getProperties']);
        financeServiceSpy  = jasmine.createSpyObj('FinanceService',  ['getAccounts']);

        positionServiceSpy.getPositions.and.returnValue(of(noPositions));
        propertyServiceSpy.getProperties.and.returnValue(of(noProperties));
        financeServiceSpy.getAccounts.and.returnValue(of([]));

        await TestBed.configureTestingModule({
            imports: [InvestmentHubComponent, NoopAnimationsModule, RouterTestingModule],
            providers: [
                { provide: PositionService, useValue: positionServiceSpy },
                { provide: PropertyService, useValue: propertyServiceSpy },
                { provide: FinanceService,  useValue: financeServiceSpy  }
            ],
            schemas: [NO_ERRORS_SCHEMA]
        }).compileComponents();

        fixture   = TestBed.createComponent(InvestmentHubComponent);
        component = fixture.componentInstance;
    });

    // ── Super removed from holdings table ────────────────────────────────────

    it('should not include SUPER rows in allHoldings', () => {
        const superAccount = makeAccount({ assetClass: 'SUPER', currentBalance: 80_000 });
        financeServiceSpy.getAccounts.and.returnValue(of([superAccount]));

        fixture.detectChanges();

        expect(component.allHoldings.every(r => r.category !== 'Super')).toBeTrue();
    });

    it('should compute superValue from SUPER accounts, not from holdings rows', () => {
        const superAccount = makeAccount({ assetClass: 'SUPER', currentBalance: 80_000 });
        financeServiceSpy.getAccounts.and.returnValue(of([superAccount]));

        fixture.detectChanges();

        expect(component.superValue).toBe(80_000);
    });

    it('investableValue should equal totalValue when super is excluded from rows', () => {
        const superAccount   = makeAccount({ assetClass: 'SUPER',    currentBalance: 50_000 });
        const vehicleAccount = makeAccount({ assetClass: 'VEHICLE',  currentBalance: 30_000 });
        financeServiceSpy.getAccounts.and.returnValue(of([superAccount, vehicleAccount]));

        fixture.detectChanges();

        // investableValue should equal totalValue (which only includes non-super rows)
        expect(component.investableValue).toBe(component.totalValue);
        expect(component.investableValue).toBe(30_000);
    });

    it('totalValue should not include super balance', () => {
        const superAccount = makeAccount({ assetClass: 'SUPER',   currentBalance: 50_000 });
        const stockAccount = makeAccount({ assetClass: 'VEHICLE', currentBalance: 20_000 });
        financeServiceSpy.getAccounts.and.returnValue(of([superAccount, stockAccount]));

        fixture.detectChanges();

        expect(component.totalValue).toBe(20_000);
    });

    // ── Vehicle/Other — cost basis falls back to value → 0 gain/loss ─────────

    it('should set costBasis = value for vehicle accounts with no cost basis', () => {
        const vehicleAccount = makeAccount({ assetClass: 'VEHICLE', currentBalance: 25_000 });
        financeServiceSpy.getAccounts.and.returnValue(of([vehicleAccount]));

        fixture.detectChanges();

        const vehicleRow = component.allHoldings.find(r => r.category === 'Other');
        expect(vehicleRow).toBeDefined();
        expect(vehicleRow!.costBasis).toBe(25_000);
        expect(vehicleRow!.gainLoss).toBe(0);
        expect(vehicleRow!.gainLossPercent).toBe(0);
    });

    // ── Positions — 0 cost basis falls back to value → 0 gain/loss ───────────

    it('should show 0 gain/loss for positions with 0 costBasisTotal', () => {
        const position = makePosition({ currentValue: 5000, costBasisTotal: 0, unrealizedGainLoss: 0, unrealizedGainLossPercent: 0 });
        positionServiceSpy.getPositions.and.returnValue(of([position]));

        fixture.detectChanges();

        const row = component.allHoldings[0];
        expect(row.gainLoss).toBe(0);
        expect(row.gainLossPercent).toBe(0);
    });
});

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeAccount(overrides: Partial<AccountDto>): AccountDto {
    return {
        id: crypto.randomUUID(),
        name: 'Test Account',
        accountType: 'ASSET',
        currency: 'AUD',
        currentBalance: 0,
        assetClass: null,
        ...overrides
    } as AccountDto;
}

function makePosition(overrides: Partial<Position>): Position {
    return {
        id: crypto.randomUUID(),
        accountId: crypto.randomUUID(),
        accountName: 'Test Account',
        instrumentId: crypto.randomUUID(),
        instrumentSymbol: 'TEST',
        instrumentName: 'Test Instrument',
        instrumentType: 'STOCK',
        quantity: 100,
        unit: 'UNIT',
        costBasisTotal: undefined,
        costBasisPerUnit: undefined,
        currentPrice: undefined,
        currentValue: undefined,
        unrealizedGainLoss: undefined,
        unrealizedGainLossPercent: undefined,
        currency: 'AUD',
        openedAt: new Date().toISOString(),
        createdAt: new Date().toISOString(),
        ...overrides
    } as unknown as Position;
}
