import type { TimeGranularityOption, WeekStartOption } from './TimeAggregatedTableTypes'

/**
 * Get user's locale from browser or return default
 */
export function getUserLocale(): string {
  return navigator.language || 'en-US'
}

/**
 * Calculate ISO 8601 week number respecting configured week start day
 * Uses FirstFourDayWeek rule similar to C# CalendarWeekRule.FirstFourDayWeek
 */
function getWeekNumber(date: Date, weekStart: WeekStartOption): number {
  // Map WeekStartOption to JavaScript day number (0=Sunday, 1=Monday, etc.)
  const weekStartDayMap: Record<WeekStartOption, number> = {
    Sunday: 0,
    Monday: 1,
    Tuesday: 2,
    Wednesday: 3,
    Thursday: 4,
    Friday: 5,
    Saturday: 6,
  }

  const firstDayOfWeek = weekStartDayMap[weekStart]

  // Clone date to avoid mutation
  const target = new Date(date.getTime())

  // Adjust to the start of the week
  const dayNum = target.getDay()
  const diff = (dayNum + 7 - firstDayOfWeek) % 7
  target.setDate(target.getDate() - diff)

  // Get first day of year
  const yearStart = new Date(target.getFullYear(), 0, 1)

  // Calculate week number using FirstFourDayWeek rule
  // This matches the C# CalendarWeekRule.FirstFourDayWeek behavior
  const dayOfYear = Math.floor((target.getTime() - yearStart.getTime()) / (24 * 60 * 60 * 1000))
  const weekNum = Math.ceil((dayOfYear + 1) / 7)

  return weekNum
}

/**
 * Format date label based on granularity and level using user's locale
 * @param startTime ISO 8601 string from backend
 * @param granularity Time granularity
 * @param level Nesting level (0 = full, >0 = abbreviated)
 * @param weekStart Week start day (for weekly granularity)
 * @param locale User's locale (default: browser locale)
 */
export function formatTimeLabel(
  startTime: string,
  granularity: TimeGranularityOption,
  level: number,
  weekStart: WeekStartOption = 'Monday',
  locale?: string
): string {
  const date = new Date(startTime)
  const userLocale = locale || getUserLocale()
  console.log('navigator.language', navigator.language)

  switch (granularity) {
    case 'Yearly':
      return new Intl.DateTimeFormat(userLocale, { year: 'numeric' }).format(date)

    case 'Quarterly': {
      const quarter = Math.floor(date.getMonth() / 3) + 1
      const year = date.getFullYear()
      return `Q${quarter} ${year}`
    }

    case 'Monthly':
      if (level === 0) {
        // Full: "January 2025"
        return new Intl.DateTimeFormat(userLocale, {
          month: 'long',
          year: 'numeric'
        }).format(date)
      } else {
        // Abbreviated: "January"
        return new Intl.DateTimeFormat(userLocale, {
          month: 'long'
        }).format(date)
      }

    case 'Weekly': {
      const weekNum = getWeekNumber(date, weekStart)
      const year = date.getFullYear()
      return `Week ${weekNum.toString().padStart(2, '0')} ${year}`
    }

    case 'Daily':
      return new Intl.DateTimeFormat(userLocale, {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit'
      }).format(date)

    default:
      return date.toLocaleDateString(userLocale)
  }
}
