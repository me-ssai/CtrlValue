export const AU_BANKS = [
    'ANZ', 'Commonwealth Bank', 'Westpac', 'NAB', 'Macquarie Bank',
    'ING', 'Bendigo Bank', 'Bank of Queensland', 'Suncorp Bank',
    'HSBC', 'Citibank', 'St.George', 'BankWest', 'ME Bank',
    'AMP Bank', 'Great Southern Bank', 'uBank', 'Up', 'Revolut',
    'Rabobank', 'RACQ Bank', 'Teachers Mutual Bank', 'Heritage Bank',
    'Newcastle Permanent', 'P&N Bank', 'Beyond Bank'
];

export const AU_BROKERS = [
    'CommSec', 'SelfWealth', 'Stake', 'eToro', 'Superhero',
    'Pearler', 'Interactive Brokers', 'nabtrade', 'ANZ Share Investing',
    'Westpac Online Investing', 'CMC Markets', 'IG Markets',
    'Bell Direct', 'Moomoo', 'Tiger Brokers', 'BullionVault',
    'ABC Bullion', 'The Perth Mint', 'Binance', 'CoinSpot', 'Swyftx',
    'Independent Reserve', 'Kraken', 'Coinbase'
];

export const AU_SUPER_FUNDS = [
    'AustralianSuper', 'Australian Retirement Trust', 'Aware Super',
    'UniSuper', 'REST Super', 'HESTA', 'Hostplus', 'Cbus',
    'Vanguard Super', 'MLC MasterKey', 'AMP Super', 'Colonial First State',
    'BT Super', 'IOOF', 'Mercer Super', 'Media Super',
    'Spirit Super', 'Brighter Super', 'Insignia Financial',
    'Netwealth', 'HUB24', 'Macquarie Super'
];

export const AU_LENDERS = [
    'ANZ', 'Commonwealth Bank', 'Westpac', 'NAB', 'Macquarie Bank',
    'ING', 'Bendigo Bank', 'Bank of Queensland', 'Suncorp Bank',
    'St.George', 'BankWest', 'Pepper Money', 'Latitude Financial',
    'Harmoney', 'MoneyPlace', 'SocietyOne', 'Wisr', 'Plenti',
    'NOW Finance', 'Liberty Financial', 'Resimac', 'Firstmac',
    'Afterpay', 'Zip', 'Humm', 'Brighte'
];

export function filterInstitutions(list: string[], term: string): string[] {
    const lower = (term ?? '').toLowerCase().trim();
    if (!lower) return list;
    return list.filter(s => s.toLowerCase().includes(lower));
}

export function getInstitutionList(accountType: string, assetClass: string): string[] {
    if (accountType === 'LIABILITY') return AU_LENDERS;
    switch (assetClass) {
        case 'CASH':                          return AU_BANKS;
        case 'STOCK': case 'ETF':
        case 'CRYPTO': case 'METAL':          return AU_BROKERS;
        case 'SUPER':                         return AU_SUPER_FUNDS;
        default:                              return [...new Set([...AU_BANKS, ...AU_BROKERS])];
    }
}
