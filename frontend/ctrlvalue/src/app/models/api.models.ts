export * from '../services/api.generated';
import {
    AccountDto,
    AccountHoldingDto,
    AccountSummaryDto,
    BudgetDto,
    CategoryDto,
    CreateAccountRequest,
    DepreciationScheduleDto,
    EntityDto,
    EntityUserDto,
    InstrumentDto,
    PositionDto,
    PositionPerformanceDto,
    PriceHistoryDto,
    TransactionDto,
    UpdateAccountRequest,
    ValuationDto
} from '../services/api.generated';

// Type Aliases for backward compatibility
export type Account = AccountDto;
export type AccountSummary = AccountSummaryDto;
export type AccountHolding = AccountHoldingDto;
export type Budget = BudgetDto;
export type Category = CategoryDto;
export type DepreciationSchedule = DepreciationScheduleDto;
export type Entity = EntityDto;
export type EntityUser = EntityUserDto;
export type Instrument = InstrumentDto;
export type Position = PositionDto;
export type PositionPerformance = PositionPerformanceDto;
export type PriceHistory = PriceHistoryDto;
export type Transaction = TransactionDto; // Mapping old Transaction to TransactionDto (which was TransactionNew)
export type TransactionNew = TransactionDto; // Mapping TransactionNew to TransactionDto
export type Valuation = ValuationDto;

// Mapped types with extensions for compatibility
export interface Asset extends AccountDto {
    currentValue: number; // Enforced as number (mapped from 0 if missing)
    category?: string;    // Mapped from assetClass?
    description?: string; // Mapped from notes?
}

export interface Liability extends AccountDto {
    currentValue: number;
    outstandingAmount?: number; // Alias for currentValue?
    interestRate?: number;    // Not in AccountDto, maybe in notes?
    category?: string;
    description?: string;
}

// Request Aliases
export type CreateAssetRequest = CreateAccountRequest;
export type UpdateAssetRequest = UpdateAccountRequest;
export type CreateLiabilityRequest = CreateAccountRequest;
export type UpdateLiabilityRequest = UpdateAccountRequest;

// Legacy types explicitly removed? -> Restored above for compatibility
// Asset and Liability are replaced by Account.
// If components use Asset/Liability, they will break.
// I can try to map them to AccountDto if they match enough, but likely better to fail fast.

// Note: Ensure component code handles Date objects for date fields instead of strings.

// ── Auth types (not in generated client) ────────────────────────────────────
export interface UserInfo {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    isEmailConfirmed?: boolean;
    role?: string;
    onboardingCompleted?: boolean;
}

export interface AuthResponse {
    token?: string;
    refreshToken?: string;
    expiration?: string;
    user?: UserInfo;
    requiresEmailVerification?: boolean;
}

export interface LoginRequest {
    email: string;
    password: string;
}

export interface RegisterRequest {
    email: string;
    password: string;
    firstName: string;
    lastName: string;
}

export interface RefreshTokenRequest {
    token: string;
    refreshToken: string;
}

export interface UpdateProfileRequest {
    firstName: string;
    lastName: string;
}

export interface UpdateProfileResponse {
    user: UserInfo;
}

export interface ChangePasswordRequest {
    currentPassword: string;
    newPassword: string;
}

export interface ResendVerificationRequest {
    email: string;
}

// ── Category Keyword Rule types ──────────────────────────────────────────────
export enum KeywordMatchType {
    Contains = 'Contains',
    Exact = 'Exact',
    StartsWith = 'StartsWith',
    Regex = 'Regex'
}

export interface CategoryKeywordRule {
    id: string;
    entityId: string;
    categoryId: string;
    categoryName: string;
    keyword: string;
    normalizedKeyword: string;
    matchType: KeywordMatchType;
    isCaseSensitive: boolean;
    createdAt: string;
    updatedAt?: string;
}

export interface CreateCategoryKeywordRuleRequest {
    categoryId: string;
    keyword: string;
    matchType: KeywordMatchType;
    isCaseSensitive: boolean;
}

export interface UpdateCategoryKeywordRuleRequest {
    categoryId: string;
    keyword: string;
    matchType: KeywordMatchType;
    isCaseSensitive: boolean;
}

// ── Account Keyword Rule types ───────────────────────────────────────────────
export interface AccountKeywordRule {
    id: string;
    entityId: string;
    accountId: string;
    accountName: string;
    keyword: string;
    normalizedKeyword: string;
    matchType: KeywordMatchType;
    isCaseSensitive: boolean;
    createdAt: string;
    updatedAt?: string;
}

export interface CreateAccountKeywordRuleRequest {
    accountId: string;
    keyword: string;
    matchType: KeywordMatchType;
    isCaseSensitive: boolean;
}

export interface UpdateAccountKeywordRuleRequest {
    accountId: string;
    keyword: string;
    matchType: KeywordMatchType;
    isCaseSensitive: boolean;
}
