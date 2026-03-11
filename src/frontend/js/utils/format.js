/**
 * format.js - Display formatting utilities
 *
 * All functions are pure (no side effects) so they are independently testable.
 */

/**
 * Format a number as Japanese yen (e.g. 1500 → "¥1,500")
 * @param {number} amount
 * @returns {string}
 */
export function formatCurrency(amount) {
  if (amount == null || isNaN(amount)) return '¥0';
  return `¥${Number(amount).toLocaleString('ja-JP')}`;
}

/**
 * Format an ISO date string or Date object as "YYYY/MM/DD" (Japanese locale)
 * @param {string|Date} date
 * @returns {string}
 */
export function formatDate(date) {
  if (!date) return '';
  const d = typeof date === 'string' ? new Date(date) : date;
  if (isNaN(d.getTime())) return '';
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}/${m}/${day}`;
}

/**
 * Format an ISO date string as "YYYY-MM" (for month filter inputs)
 * @param {string|Date} date
 * @returns {string}
 */
export function formatYearMonth(date) {
  if (!date) return '';
  const d = typeof date === 'string' ? new Date(date) : date;
  if (isNaN(d.getTime())) return '';
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  return `${y}-${m}`;
}

/**
 * Return today's date as "YYYY-MM-DD" string (for date input default values)
 * @returns {string}
 */
export function todayIso() {
  const d = new Date();
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/**
 * Return the current month as "YYYY-MM" string
 * @returns {string}
 */
export function currentYearMonth() {
  return formatYearMonth(new Date());
}

/**
 * Parse a "YYYY-MM" string into { year: number, month: number }
 * @param {string} yearMonth  e.g. "2026-03"
 * @returns {{ year: number, month: number }}
 */
export function parseYearMonth(yearMonth) {
  const [year, month] = (yearMonth || '').split('-').map(Number);
  return { year: year || new Date().getFullYear(), month: month || new Date().getMonth() + 1 };
}
