export const mockCategories = [
  { id: 1, name: '食費', color: '#F59E0B', isSystem: true },
  { id: 2, name: '交通費', color: '#3B82F6', isSystem: true },
  { id: 3, name: '娯楽', color: '#8B5CF6', isSystem: true },
  { id: 4, name: '日用品', color: '#10B981', isSystem: true },
  { id: 5, name: '医療費', color: '#EF4444', isSystem: true },
  { id: 6, name: '光熱費', color: '#F97316', isSystem: true },
  { id: 7, name: '通信費', color: '#06B6D4', isSystem: true },
  { id: 8, name: 'その他', color: '#6B7280', isSystem: true },
];

export const mockExpenses = [
  { id: 1, categoryId: 1, categoryName: '食費', categoryColor: '#F59E0B', amount: 1200, description: 'スーパーマーケット', date: '2026-03-08', createdAt: '2026-03-08T10:00:00Z' },
  { id: 2, categoryId: 2, categoryName: '交通費', categoryColor: '#3B82F6', amount: 550, description: 'Suica チャージ', date: '2026-03-07', createdAt: '2026-03-07T09:00:00Z' },
  { id: 3, categoryId: 3, categoryName: '娯楽', categoryColor: '#8B5CF6', amount: 1800, description: '映画', date: '2026-03-06', createdAt: '2026-03-06T20:00:00Z' },
  { id: 4, categoryId: 7, categoryName: '通信費', categoryColor: '#06B6D4', amount: 8000, description: '携帯料金', date: '2026-03-05', createdAt: '2026-03-05T00:00:00Z' },
  { id: 5, categoryId: 1, categoryName: '食費', categoryColor: '#F59E0B', amount: 2500, description: 'レストラン', date: '2026-03-04', createdAt: '2026-03-04T19:00:00Z' },
];

export const mockSubscriptions = [
  { id: 1, categoryId: 3, categoryName: '娯楽', serviceName: 'Netflix', amount: 1490, billingCycle: 'monthly', nextBillingDate: '2026-03-15', isActive: true },
  { id: 2, categoryId: 3, categoryName: '娯楽', serviceName: 'Spotify', amount: 980, billingCycle: 'monthly', nextBillingDate: '2026-03-20', isActive: true },
  { id: 3, categoryId: 7, categoryName: '通信費', serviceName: 'iCloud 50GB', amount: 130, billingCycle: 'monthly', nextBillingDate: '2026-03-10', isActive: true },
];

export const mockDashboardSummary = {
  currentMonthTotal: 45320,
  previousMonthTotal: 52100,
  monthOverMonthChange: -13.0,
  topCategories: [
    { categoryId: 1, categoryName: '食費', categoryColor: '#F59E0B', totalAmount: 18500, count: 12, percentage: 40.8 },
    { categoryId: 7, categoryName: '通信費', categoryColor: '#06B6D4', totalAmount: 8000, count: 1, percentage: 17.7 },
    { categoryId: 3, categoryName: '娯楽', categoryColor: '#8B5CF6', totalAmount: 7500, count: 4, percentage: 16.6 },
    { categoryId: 2, categoryName: '交通費', categoryColor: '#3B82F6', totalAmount: 6200, count: 8, percentage: 13.7 },
    { categoryId: 4, categoryName: '日用品', categoryColor: '#10B981', totalAmount: 5120, count: 5, percentage: 11.3 },
  ],
  recentExpenses: mockExpenses.slice(0, 5),
};
